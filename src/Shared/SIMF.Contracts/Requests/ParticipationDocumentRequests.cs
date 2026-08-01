using SIMF.Common.Enums;

namespace SIMF.Contracts.Requests;

/// <summary>D-500 (Wave 5, الطلبات "طلب وثيقة المشاركة") — login-required body for
/// <c>POST /app/document-requests</c>.</summary>
public sealed class SubmitParticipationDocumentRequestBody
{
    public ParticipationDocumentType DocumentType { get; set; }

    /// <summary>Optional free-text note (≤1000 chars).</summary>
    public string? Note { get; set; }
}

/// <summary>D-500 — response after a successful participation-document
/// submission.</summary>
public sealed record ParticipationDocumentRequestSubmitted(
    Guid Id,
    ParticipationDocumentType DocumentType,
    MeetingRequestStatus Status,
    DateTime CreatedAt);

/// <summary>D-500 — one row in the admin participation-document-requests grid.
/// The requester display name is resolved from the App-DB profile; the email is
/// deliberately NOT on the list row (it moves to the detail — the D-185
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

/// <summary>D-500 — single-record detail for the admin respond modal. Includes
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

/// <summary>D-500 — admin moves the row off Pending. Status must be Accepted or
/// Rejected. Open for inheritance so the route-binding endpoint can carry an
/// <c>Id</c> field (the D-168 pattern).</summary>
public class RespondToParticipationDocumentRequestRequest : RespondToRequest
{
}
