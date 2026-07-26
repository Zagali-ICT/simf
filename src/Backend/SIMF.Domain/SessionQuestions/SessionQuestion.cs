using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SessionQuestions;

/// <summary>
/// D-169 (gap doc G6, PDF §2.7.2 + §2.10) — one audience-submitted
/// question on a live <see cref="Session"/>. Submitted by an approved
/// attendee from the mobile app; surfaced to the per-session
/// <see cref="SessionModerator"/> who can hide, reorder, or "push"
/// the question to the speaker (mark it as displayed on stage).
///
/// <para><b>Geofence:</b> the submission endpoint is intended to be
/// gated by §G7 venue geofence (lat/lon polygon or WiFi SSID) once
/// G-OI-2 is resolved. Until then submission requires only an
/// authenticated approved account. The geofence is a defence-in-depth
/// layer, not the only one.</para>
///
/// <para><b>Moderator vs MobileAppRole.Moderator:</b> per gap doc §4.2
/// these are two distinct concepts and must not share a label. The
/// per-session moderator grant lives on <see cref="SessionModerator"/>;
/// the broader <c>MobileAppRole.Moderator</c> is unrelated mobile-app
/// content authority.</para>
/// </summary>
public sealed class SessionQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="Session"/> on the App DB.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Logical FK to <c>SimfUser.Id</c> on the Identity DB —
    /// no SQL constraint (cross-DB), enforced at write time.</summary>
    public Guid SubmittedByUserId { get; set; }

    /// <summary>The question text (≤1000 chars; trimmed).</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>D-174 (gap doc G11, Mockup page 26) — the addressee
    /// chosen by the audience member from the live-stream pills
    /// (Speaker vs Host). Defaults to <see cref="SessionQuestionRecipient.Speaker"/>
    /// for rows created before the column existed.</summary>
    public SessionQuestionRecipient Recipient { get; set; } = SessionQuestionRecipient.Speaker;

    /// <summary>Display order in the moderator's queue (0 = top).
    /// Updated by the moderator via the reorder endpoint; new
    /// questions arrive at the bottom of the queue.</summary>
    public int Order { get; set; }

    /// <summary>True once a moderator has hidden the question (off-
    /// topic, duplicate, abusive). Hidden questions are still
    /// retained for audit but not displayed.</summary>
    public bool IsHidden { get; set; }

    /// <summary>True once the moderator has "pushed" the question to
    /// the speaker (i.e. displayed it on stage / read it out). The
    /// timestamp lives on <see cref="PushedAt"/>.</summary>
    public bool IsPushed { get; set; }

    /// <summary>When the moderator pushed the question; null while
    /// the question is still in the queue.</summary>
    public DateTimeOffset? PushedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>P3.3 — D-212: pre-session vs live question. Set at submit time
    /// from the session's start.</summary>
    public QuestionPhase Phase { get; set; } = QuestionPhase.Live;

    /// <summary>P3.3 — D-212: the 3-stage pipeline state. New questions are
    /// <see cref="QuestionStatus.Pending"/> until the Scientific Committee
    /// approves or hides them.</summary>
    public QuestionStatus Status { get; set; } = QuestionStatus.Pending;

    /// <summary>D-771 — the <see cref="Status"/> the row held immediately before
    /// it was hidden, so un-hiding puts it back exactly where it was instead of
    /// promoting it. A Committee-rejected question returns to
    /// <see cref="QuestionStatus.Pending"/> (back in the Committee queue, NOT on
    /// the moderator desk) and an answered one returns to
    /// <see cref="QuestionStatus.Answered"/>. Null whenever the row is not hidden,
    /// and on rows hidden before this column existed (those fall back to
    /// <see cref="QuestionStatus.Approved"/>, the old behaviour).</summary>
    public QuestionStatus? StatusBeforeHidden { get; set; }

    /// <summary>P3.3 — D-212 / P4.2: the AI advisory verdict (free-text bucket,
    /// e.g. "clean" / "flagged"). Advisory only — never auto-hides. Null until
    /// the question AI filter is wired (P4.2).</summary>
    public string? AiFilterVerdict { get; set; }

    /// <summary>P3.3 — D-212: when the Committee escalates "to a team", the role
    /// name it is routed to (a role, not an individual). Null unless escalated.</summary>
    public string? AssignedToRole { get; set; }

    /// <summary>P3.3 — D-212: the Committee member who escalated. Bare Guid,
    /// cross-context (Identity DB), no FK (D-157). Null unless escalated.</summary>
    public Guid? EscalatedByUserId { get; set; }

    /// <summary>P3.3 — D-212: when the question was escalated. Null unless escalated.</summary>
    public DateTimeOffset? EscalatedAt { get; set; }
}

/// <summary>
/// D-169 (gap doc G6, PDF §2.7.2) — a per-session moderator grant.
/// Composite PK (SessionId, UserId). Distinct from
/// <c>MobileAppRole.Moderator</c> (the broader mobile-app content
/// authority introduced by D-161 — see gap doc §4.2).
///
/// <para>Admins assign moderators to a specific <see cref="Session"/>
/// via the admin CRUD. A user can moderate several sessions; a session
/// can have several moderators. Administrator role bypasses the per-
/// session grant — admins can moderate any live Q&amp;A.</para>
/// </summary>
public sealed class SessionModerator
{
    /// <summary>The session the user moderates. Real FK — same DbContext.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The moderator user. Logical FK to <c>SimfUser.Id</c> on
    /// the Identity DB — no SQL constraint (cross-DB).</summary>
    public Guid UserId { get; set; }

    /// <summary>The admin who assigned this grant. Logical FK to
    /// <c>SimfUser.Id</c>; persisted for audit.</summary>
    public Guid AssignedByUserId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
}
