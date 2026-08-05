using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// An approved attendee asking the organiser to change the job title printed on
/// their badge. Accepting one writes <see cref="RequestedJobTitle"/> onto the
/// requester's profile, which lives in the same database. The requester can
/// cancel their own request while it is still pending.
/// </summary>
public sealed class BadgeUpdateRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Who submitted the request, and whose profile is updated on
    /// accept. A bare Guid resolved on read: the user lives in the Identity
    /// database.</summary>
    public Guid RequestedByUserId { get; set; }

    public string RequestedJobTitle { get; set; } = string.Empty;

    /// <summary>The requester's job title as it stood at submit time, so the
    /// admin desk can show before and after without reading the live profile.
    /// Null when they had no job title.</summary>
    public string? CurrentJobTitle { get; set; }

    /// <summary>A free-text reason from the requester.</summary>
    public string? Note { get; set; }

    /// <summary>Pending on create, Accepted or Rejected once an admin reviews
    /// it, Cancelled when the requester withdraws it.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>An admin note shown back to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the admin moved the row off Pending; null until then.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>The admin who responded; null until one has.</summary>
    public Guid? RespondedByUserId { get; set; }
}
