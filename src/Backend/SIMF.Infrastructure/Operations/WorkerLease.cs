// Tests: SIMF.Api.Tests/Operations/WorkerLeaseTests.cs
//        (inert without a SQL Server connection string; single grant; the
//         leased decorator never blocks host startup and stops a worker it
//         never started.)
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
/// each worker idempotent (<c>Session.ReminderSentUtc</c> and its siblings) are
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
/// <para>Deliberately inert when the connection string is absent or is not SQL
/// Server. Development and the test host run one process against SQLite, where
/// there is nothing to elect and <c>sp_getapplock</c> does not exist, so the
/// lease grants immediately rather than leaving every worker switched off.</para></summary>
internal sealed class WorkerLease : BackgroundService
{
    // Any instance may hold it, so the name is the estate's, not the node's.
    private const string ResourceName = "SIMF.BackgroundWorkers";

    // Failover latency when a holder dies, and the retry cadence for a follower.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly string _connectionString;
    private readonly ILogger<WorkerLease> _logger;
    private readonly TaskCompletionSource _granted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private SqlConnection? _connection;

    public WorkerLease(IConfiguration configuration, ILogger<WorkerLease> logger)
    {
        _connectionString = configuration.GetConnectionString("SimfAppDb") ?? string.Empty;
        _logger = logger;
    }

    /// <summary>Completes once this instance owns the workers. A follower's task
    /// stays pending until the current holder goes away, so a worker gated on it
    /// simply never starts here.</summary>
    public Task Granted => _granted.Task;

    /// <summary>True once the lock is held, or immediately when the lease is
    /// inert. Read by the leased decorator for its log line only.</summary>
    public bool IsHolder { get; private set; }

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
            Grant();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!IsHolder)
            {
                await TryAcquireAsync(stoppingToken);
            }
            else if (_connection?.State != System.Data.ConnectionState.Open)
            {
                // The lock died with the connection. Another instance may already
                // have taken over, so stand down rather than assume we still hold
                // it: the workers this process started are stopped by the host on
                // shutdown, and until then a second holder is the failure this
                // whole class exists to prevent.
                _logger.LogError(
                    "Worker lease connection dropped. This instance no longer holds the "
                    + "lease; another instance will take it over within {Seconds}s.",
                    PollInterval.TotalSeconds);
                IsHolder = false;
            }

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
            _connection?.Dispose();
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
                IsHolder = true;
                Grant();
                _logger.LogInformation(
                    "Worker lease acquired. This instance runs the background workers.");
                return;
            }

            _connection.Dispose();
            _connection = null;
        }
        catch (SqlException exception)
        {
            _logger.LogWarning(
                exception,
                "Worker lease could not be acquired; retrying in {Seconds}s.",
                PollInterval.TotalSeconds);
            _connection?.Dispose();
            _connection = null;
        }
    }

    private void Grant()
    {
        IsHolder = true;
        _granted.TrySetResult();
    }

    public override void Dispose()
    {
        // Closing the connection releases the application lock, so a clean
        // shutdown hands over immediately instead of after the poll interval.
        _connection?.Dispose();
        _connection = null;
        base.Dispose();
    }
}
