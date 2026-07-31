using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// D-500 (Wave 5, الطلبات 1408:9726 "طلب تحديث البادج") — an approved attendee
/// asks the organiser to change the job title printed on their badge
/// (تغيير المسمى الوظيفي). The request carries the requested new title plus a
/// snapshot of the current one (for the admin desk). On Accept the service
/// applies <see cref="RequestedJobTitle"/> to the requester's
/// <c>UserProfile.JobTitle</c> (same App DB — no cross-DB write). Created
/// <see cref="MeetingRequestStatus.Pending"/>; the requester can cancel it
/// while still Pending. Mirrors
/// <see cref="SIMF.Domain.BusinessMeetings.SpeakerMeetingRequest"/>.
/// </summary>
public sealed class BadgeUpdateRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The authenticated user who submitted the request. Logical FK to
    /// <c>SimfUser.Id</c> on the Identity DB (bare Guid, resolved on read — no
    /// cross-DB relation, D-157). Also the profile whose JobTitle is updated on
    /// Accept.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>The requested new job title (≤128 chars, matching
    /// <c>UserProfile.JobTitle</c>).</summary>
    public string RequestedJobTitle { get; set; } = string.Empty;

    /// <summary>A snapshot of the requester's job title at submit time, so the
    /// admin desk shows the before/after without a live profile read (≤128).
    /// Null when the profile had no job title.</summary>
    public string? CurrentJobTitle { get; set; }

    /// <summary>Optional free-text reason from the requester (≤1000 chars).</summary>
    public string? Note { get; set; }

    /// <summary>Lifecycle state. Pending on create; Accepted/Rejected after an
    /// admin reviews; Cancelled when the requester withdraws it.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Optional admin response note shown to the requester (≤2000).</summary>
    public string? ResponseNote { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the admin moved the row off Pending. Null while Pending.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>The admin who responded. Logical FK (Identity); null while Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
