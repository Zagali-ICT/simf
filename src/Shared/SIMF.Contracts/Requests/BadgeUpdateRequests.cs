using SIMF.Common.Enums;

namespace SIMF.Contracts.Requests;

/// <summary>The الطلبات "طلب تحديث البادج" flow — the login-required body for
/// <c>POST /app/badge-requests</c>: the requested new badge job title.</summary>
public sealed class SubmitBadgeUpdateRequestBody
{
    /// <summary>The requested new job title (1–128 chars).</summary>
    public string RequestedJobTitle { get; set; } = string.Empty;

    /// <summary>Optional free-text reason (≤1000 chars).</summary>
    public string? Note { get; set; }
}

/// <summary>Response after a successful badge-update submission.</summary>
public sealed record BadgeUpdateRequestSubmitted(
    Guid Id,
    MeetingRequestStatus Status,
    DateTime CreatedAt);

/// <summary>One row in the admin badge-update-requests grid. The
/// requester display name is resolved from the App-DB profile; the email moves
/// to the detail, following the bulk-PII pattern: a grid never carries PII a
/// reviewer has not opened a row to see.</summary>
public sealed record AdminBadgeUpdateRequestRow(
    Guid Id,
    Guid RequestedByUserId,
    string? RequesterName,
    string RequestedJobTitle,
    string? CurrentJobTitle,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTime CreatedAt,
    DateTime? RespondedAt);

/// <summary>Single-record detail for the admin respond modal. Includes
/// <c>RequesterEmail</c> (resolved on read from the Identity DB); fetched on
/// demand and audit-logged as Viewed.</summary>
public sealed record AdminBadgeUpdateRequestDetail(
    Guid Id,
    Guid RequestedByUserId,
    string? RequesterName,
    string? RequesterEmail,
    string RequestedJobTitle,
    string? CurrentJobTitle,
    string? Note,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTime CreatedAt,
    DateTime? RespondedAt);

/// <summary>Admin moves the row off Pending. Status must be Accepted or
/// Rejected. On Accept the service applies the requested title to the
/// requester's profile. Open for inheritance so the admin endpoint can bind
/// {id} + body through a derived route class.</summary>
public class RespondToBadgeUpdateRequestRequest : RespondToRequest
{
}
