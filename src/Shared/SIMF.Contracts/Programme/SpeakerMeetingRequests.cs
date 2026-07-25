using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-269 (Mockup page 20 "Speaker profile") — login-required submission
/// body for <c>POST /api/v1/app/speakers/{speakerId}/meeting-requests</c>. The
/// affordance is shown only when the speaker's
/// <c>AllowsMeetingRequests</c> flag is true.</summary>
public sealed class SubmitSpeakerMeetingRequestRequest
{
    public string RequesterName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    /// <summary>D-474 (#11, Group G phase 1b) — the availability slot the requester
    /// picked (from <c>GET /app/speakers/{id}/available-slots</c>). When set, this is
    /// the VIP slot flow: the requester must be a VIP/VVIP and the slot must still be
    /// free. When null it is the legacy topic-only request (any approved attendee).</summary>
    public DateTimeOffset? SlotStart { get; set; }
    public DateTimeOffset? SlotEnd { get; set; }
}

/// <summary>D-269 — response after a successful speaker meeting-request
/// submission.</summary>
public sealed record SpeakerMeetingRequestSubmitted(
    Guid Id,
    Guid SpeakerId,
    MeetingRequestStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>D-269 — one row in the admin speaker-meeting-requests grid. The
/// speaker name is joined from the App DB (same context). Followed the
/// session-scoped admin-row pattern (removed D-278); the requester email is deliberately NOT
/// on the list row (it moves to <see cref="AdminSpeakerMeetingRequestDetail"/> so
/// the list endpoint does not broadcast bulk PII — the D-185 pattern).</summary>
public sealed record AdminSpeakerMeetingRequestRow(
    Guid Id,
    Guid SpeakerId,
    string SpeakerName,
    string SpeakerNameArabic,
    Guid RequestedByUserId,
    string RequesterName,
    string Subject,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);

/// <summary>D-269 — single-record detail for the admin respond modal. Includes
/// <c>RequesterEmail</c> (resolved on read from the Identity DB) so the admin can
/// reach out off-modal; fetched on demand and audit-logged as
/// <c>SpeakerMeetingRequest.Viewed</c>. Followed the session-scoped detail
/// pattern (removed D-278). D-716 appends the bound hall/table/slot (all null
/// until an accept binds one) — appended with defaults so the wire stays
/// append-only (D-219).</summary>
public sealed record AdminSpeakerMeetingRequestDetail(
    Guid Id,
    Guid SpeakerId,
    string SpeakerName,
    string SpeakerNameArabic,
    Guid RequestedByUserId,
    string RequesterName,
    string? RequesterEmail,
    string Subject,
    MeetingRequestStatus Status,
    string? ResponseNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt,
    // D-716 (item 7, GAP-2) — the hall slot the meeting was bound to on accept.
    DateTimeOffset? SlotStart = null,
    DateTimeOffset? SlotEnd = null,
    Guid? HallId = null,
    string? HallName = null,
    Guid? MeetingTableId = null,
    string? MeetingTableCode = null);

/// <summary>D-269 — admin moves the row off Pending. Status must be Accepted or
/// Rejected; Pending → Pending is rejected. Open for inheritance so the
/// route-binding endpoint can carry an <c>Id</c> field (the D-168 pattern).
/// D-716 (item 7, GAP-2) — an Accept may also bind the meeting to a free hall
/// slot: when <see cref="HallId"/> is set the picked slot
/// (<see cref="SlotStart"/>/<see cref="SlotEnd"/>, from
/// <c>GET /admin/halls/{id}/available-slots</c>) becomes the meeting time and the
/// request moves to <see cref="MeetingRequestStatus.AwaitingSpeaker"/> pending the
/// speaker's confirmation. An accept with no <see cref="HallId"/> keeps the legacy
/// straight-to-Accepted behaviour. Ignored on Reject.</summary>
public class RespondToSpeakerMeetingRequestRequest : RespondToRequest
{
    /// <summary>Optional hall to bind the accepted meeting to. When set, a free
    /// slot (<see cref="SlotStart"/>/<see cref="SlotEnd"/>) is required.</summary>
    public Guid? HallId { get; set; }

    /// <summary>Optional meeting table inside <see cref="HallId"/>.</summary>
    public Guid? MeetingTableId { get; set; }

    /// <summary>The picked hall slot start/end — required when <see cref="HallId"/>
    /// is set, must match a currently-free slot for that hall.</summary>
    public DateTimeOffset? SlotStart { get; set; }
    public DateTimeOffset? SlotEnd { get; set; }

    /// <summary>Bi-Meeting rework — the admin's 3-button model. With a bound hall:
    /// <c>false</c> = <b>Approve</b> (→ AwaitingSpeaker, mints the speaker
    /// confirmation link); <c>true</c> = <b>Confirm</b> (the admin has the other
    /// party's verbal confirmation → straight to Accepted, no link). Ignored on
    /// Reject / accept-without-hall. Append-only field (defaults false = Approve).</summary>
    public bool VerbalConfirmed { get; set; }
}
