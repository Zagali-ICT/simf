using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SessionComments;

/// <summary>
/// D-199 (Mockup page 28 — "Audience comments" / تعليقات الجمهور) —
/// one audience-submitted comment on a <see cref="Session"/>. Submitted
/// by an approved attendee from the mobile app's live-stream
/// "Audience comments" tab; surfaced to other attendees once it reaches
/// <see cref="SessionCommentStatus.Approved"/>, and to admins for
/// moderation in any state.
///
/// <para><b>Distinct from <c>SessionQuestion</c> (D-169):</b> a
/// question is addressed to the speaker/host and managed by a
/// per-session moderator queue (hide / push / reorder). A comment is a
/// public audience-feed post moderated centrally by an admin
/// (approve / hide). The two share the "audience writes about a live
/// session" shape but differ in audience surface and moderation model,
/// so they are separate entities — mirroring the codebase's choice to
/// keep MeetingRequest and SessionQuestion separate rather than forcing
/// a shared base.</para>
///
/// <para><b>AI filter (D-199 stub):</b> on submission the body is run
/// through the <c>ICommentAiFilter</c> seam, which decides the landing
/// <see cref="Status"/> (Approved or Pending). The seam is a stub in
/// this increment — the real AI moderation call is wired later behind
/// the same interface, so the service layer does not change when it
/// lands.</para>
/// </summary>
public sealed class SessionComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="Session"/> on the App DB.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Logical FK to <c>SimfUser.Id</c> on the Identity DB —
    /// no SQL constraint (cross-DB), enforced at write time. Mirrors
    /// <c>SessionQuestion.SubmittedByUserId</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>The comment text (≤1000 chars; trimmed).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Moderation state. Lands as Approved or Pending from the
    /// AI filter; a moderator can move it to Hidden (or approve a
    /// pending one) afterwards.</summary>
    public SessionCommentStatus Status { get; set; } = SessionCommentStatus.Pending;

    /// <summary>The verdict the AI filter returned at submission time,
    /// persisted for audit so a later "why was this auto-approved?"
    /// review does not need to re-run the filter. Free-text bucket
    /// (e.g. <c>approved</c>, <c>flagged</c>, <c>stub-default</c>);
    /// null for rows created before the filter ran or for admin-created
    /// rows.</summary>
    public string? AiFilterVerdict { get; set; }

    /// <summary>Soft-delete flag (CLAUDE.md §7). A deactivated comment
    /// is excluded from every list (public and admin) but the row is
    /// retained. Distinct from <see cref="SessionCommentStatus.Hidden"/>:
    /// Hidden is a moderation decision that still shows in the admin
    /// moderation list; <see cref="IsActive"/> = false is a delete.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>The moderator/admin who last changed <see cref="Status"/>.
    /// Logical FK to <c>SimfUser.Id</c>; null until first moderated.</summary>
    public Guid? ModeratedByUserId { get; set; }

    /// <summary>When <see cref="Status"/> was last changed by a
    /// moderator; null while still in its AI-filter landing state.</summary>
    public DateTimeOffset? ModeratedAt { get; set; }

    /// <summary>Soft-delete transition — sets <see cref="IsActive"/>
    /// false (CLAUDE.md §7 <c>Deactivate()</c> convention).</summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
