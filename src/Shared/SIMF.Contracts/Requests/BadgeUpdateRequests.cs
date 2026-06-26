using SIMF.Common.Enums;

namespace SIMF.Contracts.Requests;

/// <summary>D-500 (Wave 5, الطلبات "طلب تحديث البادج") — login-required body for
/// <c>POST /app/badge-requests</c>: the requested new badge job title.</summary>
public sealed class SubmitBadgeUpdateRequestBody
{
    /// <summary>The requested new job title (1–128 chars).</summary>
    public string RequestedJobTitle { get; set; } = string.Empty;

    /// <summary>Optional free-text reason (≤1000 chars).</summary>
    public string? Note { get; set; }
}

/// <summary>D-500 — response after a successful badge-update submission.</summary>
public sealed record BadgeUpdateRequestSubmitted(
    Guid Id,
    MeetingRequestStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>D-500 — one row in the admin badge-update-requests grid. The
/// requester display name is resolved from the App-DB profile; the email moves
/// to the detail (the D-185 bulk-PII pattern).</summary>
public sealed record AdminBadgeUpdateRequestRow(
    Guid Id,
    Guid RequestedByUserId,
    string? RequesterName,
    string RequestedJobTitle,
    string? CurrentJobTitle,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-500 — single-record detail for the admin respond modal. Includes
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-500 — admin moves the row off Pending. Status must be Accepted or
/// Rejected. On Accept the service applies the requested title to the
/// requester's profile. Open for inheritance (the D-168 route-binding
/// pattern).</summary>
public class RespondToBadgeUpdateRequestRequest
{
    public MeetingRequestStatus Status { get; set; }
    public string? ResponseNote { get; set; }
}
