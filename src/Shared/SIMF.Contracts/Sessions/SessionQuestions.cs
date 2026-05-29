namespace SIMF.Contracts.Sessions;

/// <summary>D-169 (gap doc G6, PDF §2.7.2) — public audience-submission
/// request body for
/// <c>POST /api/v1/sessions/{sessionId}/questions</c>. The endpoint's
/// own route shape merges this with the SessionId route param.</summary>
public sealed class SubmitSessionQuestionRequest
{
    /// <summary>The question text. Trimmed; 1–1000 chars.</summary>
    public string QuestionText { get; set; } = string.Empty;
}

/// <summary>D-169 — public-facing response after submission. The
/// queue position is the submitter's own row only (not the whole
/// queue) — moderators see the full queue via the moderator surface.</summary>
public sealed record SessionQuestionSubmitted(
    Guid Id,
    Guid SessionId,
    int Order,
    DateTimeOffset CreatedAt);

/// <summary>D-169 — one row in the moderator queue view. Submitter
/// display name + email are projected from the cross-DB lookup so the
/// moderator doesn't have to issue a second request.</summary>
public sealed record SessionQuestionModeratorRow(
    Guid Id,
    Guid SessionId,
    Guid SubmittedByUserId,
    string SubmittedByDisplayName,
    string? SubmittedByEmail,
    string QuestionText,
    int Order,
    bool IsHidden,
    bool IsPushed,
    DateTimeOffset? PushedAt,
    DateTimeOffset CreatedAt);

/// <summary>D-169 — moderator action: hide / unhide one question.
/// <see cref="IsHidden"/> is the target state (idempotent).</summary>
public sealed class SetQuestionHiddenRequest
{
    public bool IsHidden { get; set; }
}

/// <summary>D-169 — moderator action: reorder the queue. The body is
/// the full ordered list of question ids in their new positions; the
/// service replays the order column from 0..n-1.</summary>
public sealed class ReorderQuestionsRequest
{
    public IList<Guid> OrderedQuestionIds { get; set; } = new List<Guid>();
}
