using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// An approved attendee asking the organiser to change the job title printed on their
/// badge. Accepting one writes <see cref="RequestedJobTitle"/> onto the requester's
/// profile; the requester can cancel while the request is still pending.
/// </summary>
public sealed class BadgeUpdateRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid RequestedByUserId { get; set; }

    public string RequestedJobTitle { get; set; } = string.Empty;

    /// <summary>The title as it stood at submit time, so the desk shows before and after without reading the live profile.</summary>
    public string? CurrentJobTitle { get; set; }

    public string? Note { get; set; }

    /// <summary>Cancelled means the requester withdrew it.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>An admin note shown back to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public Guid? RespondedByUserId { get; set; }
}
