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
// worker off), that waiting for it never blocks host startup, and that a follower
// does not try to stop a worker it never started.
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

    /// <summary>No connection string at all: one process, nothing to elect. The
    /// lease must grant, or the test host and every Development run silently stop
    /// running background work.</summary>
    [Fact]
    public async Task The_lease_is_granted_when_no_connection_string_is_configured()
    {
        var lease = LeaseFor(null);

        await lease.StartAsync(CancellationToken.None);
        await lease.Granted.WaitAsync(TimeSpan.FromSeconds(5));

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
        await lease.Granted.WaitAsync(TimeSpan.FromSeconds(5));

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

        // The wrapper starts the worker on a continuation, so give it a moment
        // rather than asserting on a race.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (worker.Starts == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, worker.Starts);

        await leased.StopAsync(CancellationToken.None);
        await lease.StopAsync(CancellationToken.None);
        Assert.Equal(1, worker.Stops);
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
