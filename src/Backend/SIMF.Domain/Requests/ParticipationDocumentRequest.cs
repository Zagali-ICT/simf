using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// An approved attendee asking the organiser to issue a participation document. Review
/// only: an admin accepts or rejects with an optional note and issues the document out of
/// band, since nothing here generates a file. The requester can cancel while pending.
/// </summary>
public sealed class ParticipationDocumentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid RequestedByUserId { get; set; }

    public ParticipationDocumentType DocumentType { get; set; }

    public string? Note { get; set; }

    /// <summary>Cancelled means the requester withdrew it.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>An admin note shown back to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public Guid? RespondedByUserId { get; set; }
}
