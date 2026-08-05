using SIMF.Common.Enums;

namespace SIMF.Domain.Requests;

/// <summary>
/// An approved attendee asking the organiser to issue a participation document —
/// an attendance certificate, a participation letter or an invitation letter.
/// Review only: an admin accepts or rejects with an optional note and issues the
/// document out of band, since nothing here generates a file. The requester can
/// cancel their own request while it is still pending.
/// </summary>
public sealed class ParticipationDocumentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Who submitted the request. A bare Guid resolved on read: the
    /// user lives in the Identity database.</summary>
    public Guid RequestedByUserId { get; set; }

    public ParticipationDocumentType DocumentType { get; set; }

    /// <summary>A free-text note from the requester.</summary>
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
