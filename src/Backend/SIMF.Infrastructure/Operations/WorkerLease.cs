// Tests: SIMF.Api.Tests/Operations/WorkerLeaseTests.cs
//        (inert without a SQL Server connection string; a lost lease stands the
//         leased workers down and the next grant starts them again; the leased
//         decorator never blocks host startup and never stops a worker it never
//         started.)
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Operations;

/// <summary>Elects exactly one API instance to run the background workers.
///
/// <para>Every worker is registered with <c>AddHostedService</c> inside the API
/// host, so each instance of the API runs its own copy of all of them. On one
/// node that is correct. On the four the customer server requirements workbook
/// specifies it is not: the same session reminder is sent four times, four
/// no-show sweeps race for the same seats, and the once-only guards that make
/// each worker idempotent (<c>Session.ReminderSentAt</c> and its siblings) are
/// read-then-written by four processes at once, which is precisely the interval
/// they do not cover.</para>
///
/// <para>The lock is SQL Server's own application lock, taken on a dedicated
/// connection for the lifetime of the process. It needs no table and therefore
/// no migration, which matters because the persistence schema is frozen. If the
/// holder dies the connection drops, SQL Server releases the lock, and the next
/// instance to poll takes over: failover costs one poll interval and needs no
/// operator action.</para>
///
/// <para>Holding it is re-checked rather than assumed. Every poll asks the server
/// whether the lock is still ours, because each way a session-scoped application
/// lock goes away without this process dying (a server restart, a failover, a
/// connection silently re-established underneath the client) leaves
/// <c>SqlConnection.State</c> still reporting Open. When the answer is no the
/// lease stands down: <see cref="WaitForGrantAsync"/> has handed every leased
/// worker a token, that token is cancelled, the decorator stops the worker, and
/// the next acquisition starts it again. Two holders at once is the failure this
/// class exists to prevent, and detecting the loss is the only thing that stops
/// it lasting for the life of the process.</para>
///
/// <para>Deliberately inert when the connection string is absent or is not SQL
/// Server. Development and the test host run one process against SQLite, where
/// there is nothing to elect and <c>sp_getapplock</c> does not exist, so the
/// lease grants immediately rather than leaving every worker switched off.</para></summary>
internal sealed class WorkerLease : BackgroundService
{
    // Any instance may hold it, so the name is the estate's, not the node's.
    private const string ResourceName = "SIMF.BackgroundWorkers";

    // The mode sp_getapplock is asked for below, and therefore the only answer
    // from APPLOCK_MODE that still means the lock is ours.
    private const string HeldMode = "Exclusive";

    // The liveness probe is one round trip against an in-memory structure. A
    // server that has not answered it in five seconds is not healthy enough to be
    // trusted with the election either, and waiting the whole poll interval to
    // find that out only delays the handover.
    private const int ProbeTimeoutSeconds = 5;

    // Failover latency when a holder dies, and the retry cadence for a follower.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly string _connectionString;
    private readonly ILogger<WorkerLease> _logger;

    private volatile LeaseGrant _grant = new();
    private SqlConnection? _connection;

    public WorkerLease(IConfiguration configuration, ILogger<WorkerLease> logger)
    {
        _connectionString = configuration.GetConnectionString("SimfAppDb") ?? string.Empty;
        _logger = logger;
    }

    /// <summary>True while this instance holds the lock, and from the moment the
    /// lease decides it is inert. Goes back to false when the lock is lost.</summary>
    public bool IsHolder { get; private set; }

    /// <summary>Waits for the next grant of the lease and returns the token that
    /// is cancelled when <b>that</b> grant ends.
    ///
    /// <para>The wait and the token are handed out as a pair on purpose. A caller
    /// that took the wait from one grant and the token from another would either
    /// watch a token that never cancels or stop a worker that had just been
    /// legitimately restarted, and a loss signal that can be missed is worth no
    /// more than none.</para>
    ///
    /// <para>A follower's wait simply stays pending, so a worker gated on it never
    /// starts here.</para></summary>
    public async Task<CancellationToken> WaitForGrantAsync(CancellationToken cancellationToken)
    {
        var grant = _grant;
        await grant.Won.Task.WaitAsync(cancellationToken);
        return grant.Lost.Token;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsSqlServer())
        {
            // One process, no election to run. Say so once: a silent grant here
            // and a silent grant after winning a real election look identical in
            // the log, and only one of them means the workers are coordinated.
            _logger.LogInformation(
                "Worker lease is inert (no SQL Server connection string). This process "
                + "runs every background worker, which is correct for a single instance.");
            MarkHeld();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One turn of the election: acquire the lock if we do not hold it,
    /// otherwise confirm that we still do.
    ///
    /// <para>Nothing may escape this method. An exception out of
    /// <see cref="ExecuteAsync"/> stops the whole API host, so a connection-pool
    /// timeout or a transient network fault would take the node out of service
    /// rather than cost it one poll.</para></summary>
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!IsHolder)
            {
                await TryAcquireAsync(cancellationToken);
                return;
            }

            if (await StillHeldAsync(cancellationToken))
            {
                return;
            }

            _logger.LogError(
                "Worker lease lost. This instance has stood its background workers down "
                + "and will try to re-acquire in {Seconds}s.",
                PollInterval.TotalSeconds);
            await ReleaseAsync();
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogError(
                exception,
                "Worker lease poll failed; retrying in {Seconds}s.",
                PollInterval.TotalSeconds);
        }
    }

    /// <summary>A connection string that is not SQL Server cannot run
    /// <c>sp_getapplock</c>. Parsing it is how we tell, rather than reading the
    /// environment name: a test host that pointed at SQL Server should still
    /// elect, and a Production run that somehow did not should not silently
    /// behave as though it had.</summary>
    private bool IsSqlServer()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        try
        {
            return !string.IsNullOrWhiteSpace(
                new SqlConnectionStringBuilder(_connectionString).DataSource);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task TryAcquireAsync(CancellationToken cancellationToken)
    {
        try
        {
            DiscardConnection();
            _connection = new SqlConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);

            // LockOwner 'Session' ties the lock to this connection rather than to
            // a transaction, so it is held for as long as the process lives and
            // is released by SQL Server the moment the process dies. LockTimeout 0
            // makes a follower fail immediately instead of blocking the poll.
            using var command = _connection.CreateCommand();
            command.CommandText =
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', "
                + "@LockOwner = 'Session', @LockTimeout = 0";

            var result = command.CreateParameter();
            result.ParameterName = "@result";
            result.Direction = System.Data.ParameterDirection.Output;
            result.DbType = System.Data.DbType.Int32;
            command.Parameters.Add(result);

            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = ResourceName;
            command.Parameters.Add(resource);

            await command.ExecuteNonQueryAsync(cancellationToken);

            // sp_getapplock returns 0 or 1 when granted, and a negative value
            // when it is not. -1 is "timed out", which for a follower is the
            // ordinary case and not an error.
            if (result.Value is int code && code >= 0)
            {
                MarkHeld();
                _logger.LogInformation(
                    "Worker lease acquired. This instance runs the background workers.");
                return;
            }

            DiscardConnection();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Every exception, not only SqlException. Exhausting the connection
            // pool raises InvalidOperationException, and an exception escaping
            // here stops the entire API host: a node that was merely busy would
            // go out of service instead of retrying on the next poll.
            _logger.LogWarning(
                exception,
                "Worker lease could not be acquired; retrying in {Seconds}s.",
                PollInterval.TotalSeconds);
            DiscardConnection();
        }
    }

    /// <summary>Asks SQL Server whether this connection still holds the lock.
    ///
    /// <para>A round trip rather than a flag, because there is no flag worth
    /// reading. <c>SqlConnection.State</c> is cached on the client and ADO.NET
    /// only moves it off Open once an operation on that connection has failed, so
    /// a lease that issues no further commands reports Open for the life of the
    /// process, including long after a server restart, a failover or a
    /// transparently re-established connection released the session-scoped lock
    /// it is supposed to be guarding.</para>
    ///
    /// <para>Any answer other than <c>Exclusive</c>, and any failure to get an
    /// answer at all, counts as loss. Standing down while we still held it costs
    /// one poll interval of background work; staying up when we no longer do runs
    /// every worker twice for as long as the process lives.</para></summary>
    private async Task<bool> StillHeldAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            return false;
        }

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT APPLOCK_MODE('public', @resource, 'Session')";
            command.CommandTimeout = ProbeTimeoutSeconds;

            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = ResourceName;
            command.Parameters.Add(resource);

            var mode = await command.ExecuteScalarAsync(cancellationToken) as string;
            return string.Equals(mode, HeldMode, StringComparison.Ordinal);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception, "Worker lease could not be confirmed; treating it as lost.");
            return false;
        }
    }

    /// <summary>The one stand-down path. Cancels the current grant so every leased
    /// worker stops, then arms a fresh one so the next acquisition starts them
    /// again.
    ///
    /// <para>Internal rather than private so a test can drive a loss without a
    /// real failover. The class is internal already, so this widens nothing.</para></summary>
    internal async Task ReleaseAsync()
    {
        var ending = _grant;

        IsHolder = false;

        // Arm the next grant BEFORE cancelling this one. A worker that re-arms
        // the instant it is stopped has to find a grant that is pending, not the
        // cancelled one it has only just finished reacting to.
        _grant = new LeaseGrant();
        DiscardConnection();

        // CancelAsync, not Cancel: the continuations behind this token are the
        // leased workers shutting down, and running them inline would stall the
        // poll loop that has to go and re-acquire.
        await ending.Lost.CancelAsync();
    }

    private void MarkHeld()
    {
        IsHolder = true;
        _grant.Won.TrySetResult();
    }

    private void DiscardConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public override void Dispose()
    {
        // Closing the connection releases the application lock, so a clean
        // shutdown hands over immediately instead of after the poll interval.
        DiscardConnection();
        base.Dispose();
    }

    /// <summary>One grant of the lease: the task that completes when it is won,
    /// and the source that is cancelled when it is lost. Paired in a single
    /// object because a grant is repeatable rather than a one-shot latch, and a
    /// waiter must not be able to mix the two halves across generations.</summary>
    private sealed class LeaseGrant
    {
        public TaskCompletionSource Won { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Lost { get; } = new();
    }
}
