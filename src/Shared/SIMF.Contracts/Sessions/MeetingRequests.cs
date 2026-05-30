using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>D-174 (gap doc G11, Mockup page 27) — public submission
/// body for <c>POST /api/v1/sessions/{sessionId}/meeting-requests</c>.</summary>
public sealed class SubmitMeetingRequestRequest
{
    public string RequesterName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

/// <summary>D-174 — public response after a successful submission.</summary>
public sealed record MeetingRequestSubmitted(
    Guid Id,
    Guid SessionId,
    MeetingRequestStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>D-174 — one row in the admin meeting-requests grid.
/// D-185: <c>RequesterEmail</c> moved off this contract onto
/// <see cref="AdminMeetingRequestDetail"/> so the list endpoint
/// no longer broadcasts bulk PII to the CP grid. The respond modal
/// fetches the detail (with email) on click via the new GetById
/// endpoint.</summary>
public sealed record AdminMeetingRequestRow(
    Guid Id,
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    Guid RequestedByUserId,
    string RequesterName,
    string Subject,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-185 — single-record detail for the admin respond modal.
/// Includes <c>RequesterEmail</c> so the admin can reach out
/// off-modal. Fetched on demand (one record per click), audit-logged
/// as <c>MeetingRequest.Viewed</c>.</summary>
public sealed record AdminMeetingRequestDetail(
    Guid Id,
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    Guid RequestedByUserId,
    string RequesterName,
    string? RequesterEmail,
    string Subject,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-174 — admin moves the row off Pending. Status must be
/// Accepted or Rejected; Pending → Pending is rejected. Open for
/// inheritance so the route-binding endpoint can carry an
/// <c>Id</c> field (matches the D-168 pattern).</summary>
public class RespondToMeetingRequestRequest
{
    public MeetingRequestStatus Status { get; set; }
    public string? ResponseNote { get; set; }
}
