namespace SIMF.Common.Enums;

/// <summary>
/// How the recipients of an admin notification broadcast are chosen.
/// Persisted as the enum name string on <c>NotificationBroadcast.TargetMode</c>.
/// </summary>
public enum BroadcastTargetMode
{
    /// <summary>Recipients are everyone with an active seat reservation in a
    /// specific <c>Session</c> (the broadcast carries the session id).</summary>
    Session = 0,

    /// <summary>Recipients are a broad population chosen from
    /// <see cref="BroadcastAudienceScope"/> (the broadcast carries the scope).</summary>
    Audience = 1,
}
