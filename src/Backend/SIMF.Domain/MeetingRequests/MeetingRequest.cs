using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.MeetingRequests;

/// <summary>
/// D-174 (gap doc G11, Mockup page 27) — an audience-submitted request
/// for an interview / meeting during a live session. Form fields:
/// requester's name and the topic ("موضوع المقابلة"). Submitted from
/// the live-stream screen's "طلب مقابلة" pill. Goes into a Pending
/// state; an admin reviews and either Accepts or Rejects with an
/// optional response note.
/// </summary>
public sealed class MeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The session the request was made during. Real FK to
    /// <see cref="Session"/> on the App DB.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The authenticated user who submitted the request.
    /// Logical FK to <c>SimfUser.Id</c> on the Identity DB.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>The display name the requester typed into the form —
    /// may differ from their profile name if they're representing
    /// someone else.</summary>
    public string RequesterName { get; set; } = string.Empty;

    /// <summary>The meeting topic — free text up to 1000 chars.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Lifecycle state. Pending on create; Accepted or
    /// Rejected after an admin reviews.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Optional admin response note shown to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the admin moved the row off Pending. Null while
    /// still Pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>The admin who responded. Null while still Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
