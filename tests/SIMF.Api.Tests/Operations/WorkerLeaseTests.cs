// Covers: SIMF.Infrastructure/Operations/WorkerLease.cs
//         SIMF.Infrastructure/Operations/LeasedHostedService.cs
//         SIMF.Infrastructure/Operations/WorkerLeaseRegistration.cs
//
// The lease exists because every background worker is registered inside the API
// host, so each API instance runs its own copy of all of them. That is correct
// on one node and wrong on the four the customer server requirements workbook
// specifies, where the same reminder is sent four times and four sweeps race the
// once-only guards that make each worker idempotent.
//
// sp_getapplock itself is SQL Server behaviour and is not re-tested here. What is
// tested is everything around it that can be got wrong silently: that the lease
// stays inert where there is no SQL Server (the test host and Development run one
// process against SQLite, and a lease that failed closed there would switch every
// worker off), that waiting for it never blocks host startup, that a follower does
// not try to stop a worker it never started, and - the reason the grant is not a
// one-shot latch - that losing the lease stands a running worker down and the next
// grant starts it again.
//
// Losing it is driven through WorkerLease.ReleaseAsync rather than through a real
// failover, because the events that release a session-scoped application lock (a
// server restart, a failover, a silently re-established connection) cannot be
// staged in a unit test. What is proved here is the half that is ours: that the
// signal reaches every leased worker and that the worker reacts to it.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Infrastructure.Operations;
using Xunit;

namespace SIMF.Api.Tests.Operations;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class WorkerLeaseTests
{
    private static WorkerLease LeaseFor(string? connectionString)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings["ConnectionStrings:SimfAppDb"] = connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new WorkerLease(configuration, NullLogger<WorkerLease>.Instance);
    }

    /// <summary>The lease moves on continuations, so every assertion about it is
    /// about a state it reaches shortly rather than one it is already in. Polling
    /// to a deadline asserts that without racing it.</summary>
    private static async Task Eventually(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), because);
    }

    /// <summary>No connection string at all: one process, nothing to elect. The
    /// lease must grant, or the test host and every Development run silently stop
    /// running background work.</summary>
    [Fact]
    public async Task The_lease_is_granted_when_no_connection_string_is_configured()
    {
        var lease = LeaseFor(null);

        await lease.StartAsync(CancellationToken.None);
        await lease.WaitForGrantAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(lease.IsHolder);
        await lease.StopAsync(CancellationToken.None);
    }

    /// <summary>A SQLite connection string is not SQL Server and cannot run
    /// sp_getapplock. The lease detects that from the string rather than from the
    /// environment name, so a test host pointed at real SQL Server would still
    /// elect properly.</summary>
    [Fact]
    public async Task The_lease_is_granted_for_a_non_sql_server_connection_string()
    {
        var lease = LeaseFor("DataSource=:memory:");

        await lease.StartAsync(CancellationToken.None);
        await lease.WaitForGrantAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(lease.IsHolder);
        await lease.StopAsync(CancellationToken.None);
    }

    private sealed class RecordingWorker : IHostedService
    {
        public int Starts { get; private set; }
        public int Stops { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Starts++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            return Task.CompletedTask;
        }
    }

    /// <summary>The wrapper starts the worker once the lease is granted.</summary>
    [Fact]
    public async Task A_leased_worker_starts_once_the_lease_is_granted()
    {
        var lease = LeaseFor(null);
        var worker = new RecordingWorker();
        var leased = new LeasedHostedService(
            worker, lease, NullLogger<LeasedHostedService>.Instance);

        await leased.StartAsync(CancellationToken.None);
        await lease.StartAsync(CancellationToken.None);

        await Eventually(
            () => worker.Starts == 1, "The granted lease must start the leased worker.");

        await leased.StopAsync(CancellationToken.None);
        await lease.StopAsync(CancellationToken.None);
        Assert.Equal(1, worker.Stops);
    }

    /// <summary>The finding this class was rewritten for. A grant used to be a
    /// one-shot latch, so once a worker had started nothing could stop it short of
    /// host shutdown: an instance that lost the lock kept running all thirteen
    /// workers while the instance that took the lock over started its own, sending
    /// every reminder twice and releasing every no-show seat twice, indefinitely
    /// and with a clean single acquisition in both logs.</summary>
    [Fact]
    public async Task A_lost_lease_stops_the_worker_it_started()
    {
        var lease = LeaseFor(null);
        var worker = new RecordingWorker();
        var leased = new LeasedHostedService(
            worker, lease, NullLogger<LeasedHostedService>.Instance);

        await leased.StartAsync(CancellationToken.None);
        await lease.StartAsync(CancellationToken.None);
        await Eventually(() => worker.Starts == 1, "The worker must start on the grant.");

        await lease.ReleaseAsync();

        await Eventually(
            () => worker.Stops == 1,
            "Losing the lease must stop the worker: a second holder running every "
            + "worker alongside the new one is the failure the lease exists to prevent.");
        Assert.False(lease.IsHolder);

        // The stand-down already stopped it, so shutdown must not stop it twice.
        await leased.StopAsync(CancellationToken.None);
        await lease.StopAsync(CancellationToken.None);
        Assert.Equal(1, worker.Stops);
    }

    /// <summary>Standing down is not retiring. Losing the lock to a restarting
    /// server is ordinary, and this instance is a legitimate candidate for the
    /// next election, so the worker has to come back when it wins one.</summary>
    [Fact]
    public async Task A_worker_stood_down_by_a_lost_lease_starts_again_on_the_next_grant()
    {
        var lease = LeaseFor(null);
        var worker = new RecordingWorker();
        var leased = new LeasedHostedService(
            worker, lease, NullLogger<LeasedHostedService>.Instance);

        await leased.StartAsync(CancellationToken.None);
        await lease.StartAsync(CancellationToken.None);
        await Eventually(() => worker.Starts == 1, "The worker must start on the grant.");

        await lease.ReleaseAsync();
        await Eventually(() => worker.Stops == 1, "Losing the lease must stop the worker.");

        // A second run of the lease is a second election, won again.
        await lease.StartAsync(CancellationToken.None);

        await Eventually(
            () => worker.Starts == 2,
            "Re-acquiring the lease must start the worker again; a grant is repeatable, "
            + "not a latch that fires once per process.");
        Assert.True(lease.IsHolder);

        await leased.StopAsync(CancellationToken.None);
        await lease.StopAsync(CancellationToken.None);
        Assert.Equal(2, worker.Stops);
    }

    /// <summary>The load-bearing property for a follower. Waiting for a lease it
    /// will never win must not block the host's startup sequence: that would turn
    /// "this node does not run the workers" into "this node never serves a
    /// request".</summary>
    [Fact]
    public async Task Waiting_for_the_lease_does_not_block_host_startup()
    {
        // A lease that is never started never grants, which is exactly a
        // follower's position.
        var lease = LeaseFor(null);
        var worker = new RecordingWorker();
        var leased = new LeasedHostedService(
            worker, lease, NullLogger<LeasedHostedService>.Instance);

        var start = leased.StartAsync(CancellationToken.None);

        Assert.True(
            start.IsCompleted,
            "LeasedHostedService.StartAsync must return without waiting for the lease.");
        Assert.Equal(0, worker.Starts);

        await leased.StopAsync(CancellationToken.None);
    }

    /// <summary>A follower must not call StopAsync on a worker it never started.
    /// Several of these workers open a scope in StopAsync to flush, and a
    /// BackgroundService that was never started has no execute task to stop.</summary>
    [Fact]
    public async Task A_follower_does_not_stop_a_worker_it_never_started()
    {
        var lease = LeaseFor(null);
        var worker = new RecordingWorker();
        var leased = new LeasedHostedService(
            worker, lease, NullLogger<LeasedHostedService>.Instance);

        await leased.StartAsync(CancellationToken.None);
        await leased.StopAsync(CancellationToken.None);

        Assert.Equal(0, worker.Starts);
        Assert.Equal(0, worker.Stops);
    }

    /// <summary>The email drainer must stay unleased. Its EmailQueue is a bounded
    /// in-memory Channel registered per process, so a follower that queues a
    /// message has only its own drainer to send it; gating it would strand every
    /// email raised on a follower and then drop them, the channel being DropWrite
    /// when full. This pins the exclusion so a later tidy-up cannot sweep it in
    /// with the others.</summary>
    [Fact]
    public void The_email_background_service_is_not_registered_as_leased()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains(
            "services.AddHostedService<EmailBackgroundService>();",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "AddLeasedHostedService<EmailBackgroundService>",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>The poll loop must survive a failure that is not a SqlException.
    ///
    /// <para>Asserted on the source rather than by provoking one, because the
    /// exceptions that matter here cannot be staged without a live server:
    /// exhausting the connection pool raises InvalidOperationException and needs a
    /// real pool to exhaust, and the connection-string tricks that would raise one
    /// offline are thrown by SqlConnectionStringBuilder as readily as by
    /// SqlConnection, in which case the lease would decide it is not SQL Server at
    /// all and the test would prove the opposite of what it claims.</para>
    ///
    /// <para>Pinned all the same, because the cost of the narrow catch is not one
    /// failed poll. An exception escaping ExecuteAsync stops the entire API host,
    /// so a node that was merely busy would go out of service.</para></summary>
    [Fact]
    public void The_lease_poll_does_not_narrow_its_catch_to_sql_exceptions()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Operations", "WorkerLease.cs"));

        Assert.DoesNotContain("catch (SqlException", source, StringComparison.Ordinal);

        Assert.Contains(
            "catch (Exception exception) when (!cancellationToken.IsCancellationRequested)",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>The liveness probe, pinned the same way and for the same reason: a
    /// failover cannot be staged in a unit test, but a lease that reads
    /// SqlConnection.State instead of asking the server can never detect one.
    /// State is cached on the client and ADO.NET only moves it off Open after an
    /// operation has failed, so a lease that issues no further commands reports
    /// Open for the life of the process no matter what the server did.</summary>
    [Fact]
    public void The_lease_confirms_it_still_holds_the_lock_by_asking_the_server()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Operations", "WorkerLease.cs"));

        Assert.Contains("APPLOCK_MODE", source, StringComparison.Ordinal);

        Assert.DoesNotContain("ConnectionState", source, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
