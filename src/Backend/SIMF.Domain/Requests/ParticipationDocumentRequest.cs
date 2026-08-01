using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// D-500 (Wave 5, الطلبات 1408:9726 "طلب وثيقة المشاركة") — an approved
/// attendee asks the organiser to issue a participation document (an
/// attendance certificate, participation letter, or invitation letter). The
/// request is review-only: an admin Accepts or Rejects with an optional
/// response note and issues the document off-band (no file is generated this
/// wave). Created <see cref="MeetingRequestStatus.Pending"/>; the requester
/// can cancel it while still Pending. Mirrors
/// <see cref="SIMF.Domain.BusinessMeetings.SpeakerMeetingRequest"/> but with no
/// counterparty entity.
/// </summary>
public sealed class ParticipationDocumentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The authenticated user who submitted the request. Logical FK to
    /// <c>SimfUser.Id</c> on the Identity DB (bare Guid, resolved on read — no
    /// cross-DB relation, D-157).</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Which document is requested.</summary>
    public ParticipationDocumentType DocumentType { get; set; }

    /// <summary>Optional free-text note from the requester (≤1000 chars).</summary>
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
