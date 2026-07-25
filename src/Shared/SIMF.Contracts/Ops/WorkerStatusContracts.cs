namespace SIMF.Contracts.Ops;

/// <summary>
/// The health state of one background worker, derived from its last heartbeat
/// relative to its expected poll interval.
/// </summary>
public enum WorkerState
{
    /// <summary>Registered but has not completed its first tick yet (still inside
    /// its startup delay / first expected cycle).</summary>
    Starting = 0,

    /// <summary>Completed a tick within its expected window; running normally.</summary>
    Healthy = 1,

    /// <summary>No successful tick for longer than the allowed window: the worker
    /// has missed its schedule (stuck, or the host is not running it).</summary>
    Stale = 2,

    /// <summary>Its most recent tick threw. <see cref="WorkerStatus.LastError"/>
    /// carries the message; clears back to Healthy on the next successful tick.</summary>
    Faulted = 3,
}

/// <summary>
/// One background worker's live status for the CP services monitor and the
/// /health check, reported from the in-process heartbeat registry.
/// </summary>
public sealed record WorkerStatus
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required WorkerState State { get; init; }

    /// <summary>When the worker last completed a tick (success or failure).</summary>
    public DateTimeOffset? LastRun { get; init; }

    /// <summary>When the worker last completed a tick without throwing.</summary>
    public DateTimeOffset? LastSuccess { get; init; }

    /// <summary>When the worker's last failing tick threw.</summary>
    public DateTimeOffset? LastFailure { get; init; }

    /// <summary>The message of the most recent failing tick, if the worker is
    /// currently faulted; null once a later success clears it.</summary>
    public string? LastError { get; init; }

    public required long RunCount { get; init; }
    public required long FailureCount { get; init; }

    /// <summary>The worker's expected interval between ticks, in seconds; 0 for an
    /// event-driven worker (e.g. the email queue drain) that has no fixed cadence.</summary>
    public required int ExpectedIntervalSeconds { get; init; }
}

/// <summary>
/// Response for <c>GET /api/v1/admin/ops/workers</c> - every registered worker's
/// status plus a roll-up, so the CP can badge the whole tier at a glance.
/// </summary>
public sealed record WorkerStatusListResponse
{
    public required IReadOnlyList<WorkerStatus> Workers { get; init; }
    public required DateTimeOffset Generated { get; init; }

    /// <summary>Workers that are Healthy or Starting (i.e. up).</summary>
    public required int HealthyCount { get; init; }
    public required int StaleCount { get; init; }
    public required int FaultedCount { get; init; }
}
