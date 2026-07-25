namespace SIMF.Common.Enums;

/// <summary>
/// The lifecycle state of an admin notification broadcast. Persisted as the enum
/// name string on <c>NotificationBroadcast.Status</c>. The background worker only
/// ever picks up <see cref="Pending"/> rows and claims them to
/// <see cref="Processing"/> before fan-out, so a restart mid-send leaves a row
/// <see cref="Processing"/> (at-most-once — never re-sent).
/// </summary>
public enum BroadcastStatus
{
    /// <summary>Accepted by the API, awaiting the background worker.</summary>
    Pending = 0,

    /// <summary>Claimed by the worker; recipients are being dispatched.</summary>
    Processing = 1,

    /// <summary>Fan-out finished; the counters are final.</summary>
    Completed = 2,

    /// <summary>The worker failed before completing; <c>Error</c> carries why.</summary>
    Failed = 3,
}
