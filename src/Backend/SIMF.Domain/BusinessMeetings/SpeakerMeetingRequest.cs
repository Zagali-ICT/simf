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

    /// <summary>Lifecycle state. Pending on create; Accepted or Rejected
    /// after an admin reviews.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Optional admin response note shown to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the admin moved the row off Pending. Null while still
    /// Pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>The admin who responded. Null while still Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
