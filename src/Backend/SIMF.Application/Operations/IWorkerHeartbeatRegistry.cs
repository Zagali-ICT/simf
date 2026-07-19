using SIMF.Contracts.Ops;

namespace SIMF.Application.Operations;

/// <summary>
/// In-process registry that every background worker (BackgroundService) reports
/// to, so the CP services monitor and the /health check can tell which workers
/// are up. A singleton shared by the API host and its hosted workers.
///
/// <para>Today the workers run in-process inside the API host, so an in-memory
/// registry is sufficient. When they move to a dedicated worker service
/// (planned), this interface stays and the implementation swaps for a persisted
/// or scraped heartbeat that crosses the process boundary.</para>
/// </summary>
public interface IWorkerHeartbeatRegistry
{
    /// <summary>
    /// Declare a worker at startup so the monitor lists it even before its first
    /// tick. <paramref name="expectedInterval"/> drives the staleness threshold;
    /// pass <see cref="System.TimeSpan.Zero"/> for an event-driven worker with no
    /// fixed cadence. Idempotent per <paramref name="workerName"/>.
    /// </summary>
    void Register(string workerName, string description, TimeSpan expectedInterval);

    /// <summary>Record a tick that completed without throwing.</summary>
    void RecordSuccess(string workerName);

    /// <summary>Record a tick that threw; the message is surfaced to the monitor.</summary>
    void RecordFailure(string workerName, string error);

    /// <summary>
    /// A point-in-time snapshot of every registered worker's status, with each
    /// state derived from the current time.
    /// </summary>
    WorkerStatusListResponse Snapshot();
}
