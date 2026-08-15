using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SessionQuestions;

/// <summary>One audience question on a <see cref="Session"/>, moderated by the
/// Scientific Committee and the per-session <see cref="SessionModerator"/>. Until
/// the session starts any approved account may ask; from the start a hall carrying
/// a geofence needs an arrival record, and the window shuts at the end.</summary>
public sealed class SessionQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>A bare Guid, not a navigation: the user lives in the Identity database.</summary>
    public Guid SubmittedByUserId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public SessionQuestionRecipient Recipient { get; set; } = SessionQuestionRecipient.Speaker;

    /// <summary>Queue position, 0 at the top. Every submission is written 0 and the
    /// queue sorts on Order then <see cref="CreatedAt"/>, so untouched rows tie.</summary>
    public int Order { get; set; }

    /// <summary>Vestigial: never updated, visibility having moved onto
    /// <see cref="Status"/>. The engagement report still counts it, so its
    /// "hidden" KPI reads false for every row.</summary>
    public bool IsHidden { get; set; }

    // Pushed = handed to the speaker to read out on stage. Hiding clears both;
    // restoring or re-approving does not push it again.
    public bool IsPushed { get; set; }

    public DateTime? PushedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Fixed at submit, and it routes rather than labels: Pre is AI-screened
    /// for the Committee, Live goes straight to the moderator desk.</summary>
    public QuestionPhase Phase { get; set; } = QuestionPhase.Live;

    /// <summary>The pipeline state, and the single source of truth for visibility.</summary>
    public QuestionStatus Status { get; set; } = QuestionStatus.Pending;

    /// <summary>The <see cref="Status"/> held before hiding, so un-hiding restores
    /// rather than promotes: a rejected question goes back to Pending, not to the
    /// desk. An explicit approval clears it, being a decision of its own.</summary>
    public QuestionStatus? StatusBeforeHidden { get; set; }

    /// <summary>A free-text bucket such as "clean" or "flagged". Advisory only: it
    /// blocks and hides nothing. Null on a live question, only Pre being screened.</summary>
    public string? AiFilterVerdict { get; set; }

    // Written together when the Committee escalates a question, and all null on one
    // never escalated. The assignment is to a role name, never to an individual.
    public string? AssignedToRole { get; set; }

    public Guid? EscalatedByUserId { get; set; }

    public DateTime? EscalatedAt { get; set; }
}

/// <summary>A grant of moderator authority over one <see cref="Session"/>'s question
/// queue; the Administrator role bypasses it and needs no row here. <b>Not
/// <c>MobileAppRole.Moderator</c></b>, which confers nothing here.</summary>
public sealed class SessionModerator
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid UserId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAt { get; set; }
}
