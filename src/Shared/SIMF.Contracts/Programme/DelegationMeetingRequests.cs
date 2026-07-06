using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-478 (#11, Group G phase 2) — a delegate submits a request for their
/// delegation to meet another country's delegation.</summary>
public sealed class SubmitDelegationMeetingRequestRequest
{
    /// <summary>ISO 3166-1 alpha-2 code of the country to meet.</summary>
    public string TargetCountryCode { get; set; } = string.Empty;

    /// <summary>"count X" — how many of the requester's delegation will attend.</summary>
    public int AttendeeCount { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>The proposed slot (optional; the team confirms it on accept).</summary>
    public DateTimeOffset? SlotStartUtc { get; set; }
    public DateTimeOffset? SlotEndUtc { get; set; }
}

/// <summary>D-478 — the receipt after a successful submit.</summary>
public sealed record DelegationMeetingRequestSubmitted(
    Guid Id, MeetingRequestStatus Status, DateTimeOffset CreatedAt);

/// <summary>D-478 — one row on the admin delegation-meeting desk.</summary>
public sealed record AdminDelegationMeetingRequestRow(
    Guid Id,
    int RequestingCountryId,
    string RequestingCountry,
    int TargetCountryId,
    string TargetCountry,
    Guid RequestedByUserId,
    int AttendeeCount,
    string Subject,
    MeetingRequestStatus Status,
    DateTimeOffset? SlotStartUtc,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-478 — the admin detail (adds the requester email, resolved on read).</summary>
public sealed record AdminDelegationMeetingRequestDetail(
    Guid Id,
    string RequestingCountry,
    string TargetCountry,
    Guid RequestedByUserId,
    string? RequesterEmail,
    int AttendeeCount,
    string Subject,
    MeetingRequestStatus Status,
    DateTimeOffset? SlotStartUtc,
    DateTimeOffset? SlotEndUtc,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-478 — the team's Accept/Reject response.</summary>
public class RespondToDelegationMeetingRequestRequest : RespondToRequest
{
}
