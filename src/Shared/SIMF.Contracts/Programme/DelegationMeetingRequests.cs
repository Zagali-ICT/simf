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
    public DateTimeOffset? SlotStart { get; set; }
    public DateTimeOffset? SlotEnd { get; set; }
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
    DateTimeOffset? SlotStart,
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
    DateTimeOffset? SlotStart,
    DateTimeOffset? SlotEnd,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-478 + Bi-Meeting rework — the team's respond action, unified with the
/// speaker flow. <c>Status = Rejected</c> is <b>Cancel</b> (with a justification note).
/// <c>Status = Accepted</c> with a bound <see cref="HallId"/> is either <b>Approve</b>
/// (<see cref="VerbalConfirmed"/> = false → AwaitingSpeaker, awaiting the other party's
/// confirmation) or <b>Confirm</b> (<see cref="VerbalConfirmed"/> = true → Accepted, the
/// admin has the other party's verbal confirmation). Approve/Confirm require the hall +
/// a free slot from <c>GET /admin/halls/{id}/available-slots</c>.</summary>
public class RespondToDelegationMeetingRequestRequest : RespondToRequest
{
    /// <summary>The hall to bind the meeting to (required for Approve/Confirm).</summary>
    public Guid? HallId { get; set; }

    /// <summary>Optional meeting table inside <see cref="HallId"/>.</summary>
    public Guid? MeetingTableId { get; set; }

    /// <summary>The picked hall slot start/end — required when <see cref="HallId"/> is
    /// set, must match a currently-free slot for that hall.</summary>
    public DateTimeOffset? SlotStart { get; set; }
    public DateTimeOffset? SlotEnd { get; set; }

    /// <summary>Bi-Meeting rework — Approve (false) vs Confirm (true). See the class
    /// summary. Append-only field (defaults false = Approve).</summary>
    public bool VerbalConfirmed { get; set; }
}
