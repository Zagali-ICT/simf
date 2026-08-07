using SIMF.Common.Enums;

namespace SIMF.Contracts.Requests;

/// <summary>Login-required body for the الطلبات "طلب وثيقة المشاركة" request,
/// <c>POST /app/document-requests</c>.</summary>
public sealed class SubmitParticipationDocumentRequestBody
{
    public ParticipationDocumentType DocumentType { get; set; }

    /// <summary>Optional free-text note (≤1000 chars).</summary>
    public string? Note { get; set; }
}

/// <summary>Response after a successful participation-document
/// submission.</summary>
public sealed record ParticipationDocumentRequestSubmitted(
    Guid Id,
    ParticipationDocumentType DocumentType,
    MeetingRequestStatus Status,
    DateTime CreatedAt);

/// <summary>One row in the admin participation-document-requests grid.
/// The requester display name is resolved from the App-DB profile; the email is
/// deliberately NOT on the list row (it moves to the detail, following the
/// bulk-PII pattern).</summary>
public sealed record AdminParticipationDocumentRequestRow(
    Guid Id,
    Guid RequestedByUserId,
    string? RequesterName,
    ParticipationDocumentType DocumentType,
    string? Note,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTime CreatedAt,
    DateTime? RespondedAt);

/// <summary>Single-record detail for the admin respond modal. Includes
/// <c>RequesterEmail</c> (resolved on read from the Identity DB) so the admin
/// can reach out; fetched on demand and audit-logged as Viewed.</summary>
public sealed record AdminParticipationDocumentRequestDetail(
    Guid Id,
    Guid RequestedByUserId,
    string? RequesterName,
    string? RequesterEmail,
    ParticipationDocumentType DocumentType,
    string? Note,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTime CreatedAt,
    DateTime? RespondedAt);

/// <summary>Admin moves the row off Pending. Status must be Accepted or
/// Rejected. Open for inheritance so the route-binding endpoint can carry an
/// <c>Id</c> field.</summary>
public class RespondToParticipationDocumentRequestRequest : RespondToRequest
{
}
