namespace SIMF.Common.Enums;

/// <summary>
/// The broad population an admin notification broadcast targets when
/// <see cref="BroadcastTargetMode.Audience"/> is chosen. Persisted as the enum
/// name string on <c>NotificationBroadcast.AudienceScope</c>.
/// </summary>
public enum BroadcastAudienceScope
{
    /// <summary>All approved accounts that use the app (every non-Admin user
    /// whose account state is Approved).</summary>
    ApprovedAppUsers = 0,

    /// <summary>Only users who hold an active seat reservation in at least one
    /// session (i.e. attendees actually registered for the event).</summary>
    EventAttendees = 1,

    /// <summary>Every non-Admin account regardless of approval state (includes
    /// pending / unapproved sign-ups).</summary>
    EveryoneIncludingPending = 2,
}
