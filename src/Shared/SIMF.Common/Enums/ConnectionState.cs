namespace SIMF.Common.Enums;

/// <summary>
/// B6 — D-224 (additive new enum): the lifecycle of a visitor-to-visitor
/// networking <c>Connection</c>. The integer values are the persisted wire
/// value and follow the same additive-only discipline as the frozen enums in
/// this namespace: never renumber or rename an existing case; a new state
/// appends a value that does not collide.
/// </summary>
public enum ConnectionState
{
    /// <summary>The requester has asked; the target has not yet responded.</summary>
    Pending = 0,

    /// <summary>The target accepted — the two are connected.</summary>
    Accepted = 1,

    /// <summary>The target declined the request.</summary>
    Declined = 2,
}
