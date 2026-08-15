using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SessionQuestions;

/// <summary>
/// One audience-submitted question on a <see cref="Session"/>, asked from the
/// mobile app by an approved attendee and worked through the moderation pipeline
/// by the Scientific Committee and the per-session <see cref="SessionModerator"/>,
/// who can hide it, reorder it, or push it to the speaker to be read out on stage.
///
/// <para>Where the asker is standing only matters once the session is live: until
/// the start any approved account may ask; from the start a hall that carries a
/// geofence needs an arrival record for the asker. The window shuts at the end.</para>
/// </summary>
public sealed class SessionQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A real foreign key: the session lives in the same DbContext.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>A bare Guid rather than a navigation: the user lives in the
    /// Identity database, so there is no foreign key across the two.</summary>
    public Guid SubmittedByUserId { get; set; }

    /// <summary>Trimmed at submit, then 1 to 1000 characters, checked by the
    /// service and again by the column length.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Which of the two live-stream pills the asker addressed, the
    /// speaker or the host on stage. Rows written before the pills read as Speaker.</summary>
    public SessionQuestionRecipient Recipient { get; set; } = SessionQuestionRecipient.Speaker;

    /// <summary>Position in the moderator's queue, 0 at the top. Every submission
    /// is written with 0 and the queue sorts on Order then <see cref="CreatedAt"/>,
    /// so untouched rows tie and fall into arrival order; a reorder overrides that.</summary>
    public int Order { get; set; }

    /// <summary>Vestigial: written false at insert and never updated again.
    /// Visibility became <see cref="Status"/> being <see cref="QuestionStatus.Hidden"/>,
    /// which is what both moderation services set, and the hidden marker on a desk
    /// row is derived from the status at projection time.
    ///
    /// <para>The engagement report is the one reader left, and it counts this
    /// column instead of the status -- so its "hidden" KPI and its Excel column
    /// read false for every row ever written. Deriving both from the status is
    /// what lets this column go.</para></summary>
    public bool IsHidden { get; set; }

    // Pushed means the moderator handed the question to the speaker to be read out
    // on stage; PushedAt is the moment, null while it waits in the queue. Hiding a
    // question clears both, and restoring or re-approving it does not push it again.
    public bool IsPushed { get; set; }

    public DateTime? PushedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Fixed at submit against the session's start, and it routes the
    /// question rather than merely labelling it: a <see cref="QuestionPhase.Pre"/>
    /// question is AI-screened and queued for the Scientific Committee, a
    /// <see cref="QuestionPhase.Live"/> one goes straight to the moderator desk.</summary>
    public QuestionPhase Phase { get; set; } = QuestionPhase.Live;

    /// <summary>The pipeline state, and the single source of truth for visibility.
    /// A pre-session question is written <see cref="QuestionStatus.Pending"/> and
    /// waits for the Committee; a live one is written
    /// <see cref="QuestionStatus.Approved"/> and lands on the moderator desk.</summary>
    public QuestionStatus Status { get; set; } = QuestionStatus.Pending;

    /// <summary>The <see cref="Status"/> the row held immediately before it was
    /// hidden, so un-hiding puts it back exactly where it was instead of promoting
    /// it. A Committee-rejected question returns to <see cref="QuestionStatus.Pending"/>,
    /// back in the Committee queue and NOT on the moderator desk; an answered one
    /// returns to <see cref="QuestionStatus.Answered"/>. Null whenever the row is
    /// not hidden, and on rows hidden before this column existed, which fall back
    /// to <see cref="QuestionStatus.Approved"/>, the old behaviour. An explicit
    /// approval clears it too, being a decision of its own.</summary>
    public QuestionStatus? StatusBeforeHidden { get; set; }

    /// <summary>The AI screening verdict, a free-text bucket such as "clean" or
    /// "flagged". Advisory only: it blocks nothing and hides nothing, it just tags
    /// the row for the Committee to weigh. Only pre-session questions are screened,
    /// so a live question leaves it null.</summary>
    public string? AiFilterVerdict { get; set; }

    // Written together when the Committee escalates a question to a team. The
    // assignment is to a role name, never to an individual, and the escalating
    // member is a bare Guid because they live in the Identity database. All three
    // stay null on a question that was never escalated.
    public string? AssignedToRole { get; set; }

    public Guid? EscalatedByUserId { get; set; }

    public DateTime? EscalatedAt { get; set; }
}

/// <summary>
/// A grant of moderator authority over one <see cref="Session"/>'s question queue,
/// keyed on (SessionId, UserId). A user can moderate several sessions and a session
/// can have several moderators. The Administrator role bypasses the grant, so an
/// administrator can moderate any session's live Q&amp;A without a row here.
///
/// <para><b>Not <c>MobileAppRole.Moderator</c>.</b> The two share a word and nothing
/// else: that one is broad mobile-app content authority and confers nothing here.</para>
/// </summary>
public sealed class SessionModerator
{
    /// <summary>A real foreign key: the session lives in the same DbContext.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The moderator. A bare Guid rather than a navigation: the user lives
    /// in the Identity database, so there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>The admin who made the grant, kept for audit. Cross-database in
    /// the same way as <see cref="UserId"/>.</summary>
    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAt { get; set; }
}
