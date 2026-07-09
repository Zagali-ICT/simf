using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// D-269 (Mockup page 20 "Speaker profile") — an attendee's request to meet a
/// <see cref="Speaker"/> who has opted in via
/// <see cref="Speaker.AllowsMeetingRequests"/>. Distinct from the session-scoped
/// <c>MeetingRequest</c> (the screen-27 "Request interview during a live
/// session" feature, removed in D-278): this one targets a speaker, not a session. Form fields: the
/// requester's display name and the meeting topic. Created
/// <see cref="MeetingRequestStatus.Pending"/>; an admin reviews and Accepts or
/// Rejects with an optional response note (the same lifecycle enum the
/// session-scoped request uses).
/// </summary>
public sealed class SpeakerMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The speaker the request is for. Real FK to
    /// <see cref="Speaker"/> on the App DB (cascade — a deleted speaker
    /// removes its requests).</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>The authenticated user who submitted the request.
    /// Logical FK to <c>SimfUser.Id</c> on the Identity DB (bare Guid,
    /// resolved on read — no cross-DB relation, D-157).</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>The display name the requester typed into the form — may
    /// differ from their profile name if they represent someone else.</summary>
    public string RequesterName { get; set; } = string.Empty;

    /// <summary>The meeting topic — free text up to 1000 chars.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>D-474 (#11) — the slot the requester picked from the speaker's
    /// availability windows. Null for a legacy topic-only request (D-269); set
    /// for a VIP slot request. The pair is validated to fall inside an active
    /// window and to be free at submit time.</summary>
    public DateTimeOffset? SlotStartUtc { get; set; }
    public DateTimeOffset? SlotEndUtc { get; set; }

    /// <summary>D-611 (Wave B) — the <see cref="SpeakerAvailabilityWindow"/> the
    /// picked slot belongs to, now persisted as a real FK (OnDelete SetNull)
    /// instead of only being validated at submit time. Null for a legacy
    /// topic-only request or when the window is later removed.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>Lifecycle state. Pending on create; Accepted or Rejected
    /// after an admin reviews. D-716 adds AwaitingSpeaker for an accept that also
    /// bound the request to a hall slot (awaiting the speaker's own confirmation).</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>D-716 (item 7, FDS-013 §15 GAP-2) — the hall the admin bound the
    /// meeting to on Accept. Real FK to <see cref="Hall"/> on the App DB (SetNull —
    /// a deleted hall clears the binding rather than blocking). Null for a legacy
    /// accept-without-hall (the meeting is confirmed but not placed in a hall). When
    /// set, <see cref="SlotStartUtc"/>/<see cref="SlotEndUtc"/> hold the bound hall
    /// slot (Option A — the hall slot is the meeting time of record).</summary>
    public Guid? HallId { get; set; }

    /// <summary>D-716 — the optional meeting table inside <see cref="HallId"/> the
    /// meeting was placed at. Real FK to <see cref="MeetingTable"/> (SetNull). Null
    /// when the admin bound a hall but no specific table.</summary>
    public Guid? MeetingTableId { get; set; }

    /// <summary>D-716 — when the speaker confirmed (or declined) the bound meeting
    /// via the double-opt-in link. Null while <see cref="MeetingRequestStatus.AwaitingSpeaker"/>;
    /// set by Slice C's public token flow. Persisted now as an additive column.</summary>
    public DateTimeOffset? SpeakerDecisionAt { get; set; }

    /// <summary>Optional admin response note shown to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the admin moved the row off Pending. Null while still
    /// Pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>The admin who responded. Null while still Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
