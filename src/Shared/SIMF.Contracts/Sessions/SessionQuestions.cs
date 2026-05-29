using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>D-169 (gap doc G6, PDF §2.7.2) — public audience-submission
/// request body for
/// <c>POST /api/v1/sessions/{sessionId}/questions</c>. The endpoint's
/// own route shape merges this with the SessionId route param.</summary>
public sealed class SubmitSessionQuestionRequest
{
    /// <summary>The question text. Trimmed; 1–1000 chars.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>D-171 (gap doc G7, PDF §2.10) — the audience-self-asserts
    /// "I am at the venue" toggle. Owner-default resolution of G-OI-2
    /// (lat/lon vs WiFi SSID vs self-toggle) — the toggle was chosen as
    /// the simplest input source that does not require GPS permissions
    /// or a maintained venue-WiFi list. Must be <c>true</c> for the
    /// submission to be accepted.</summary>
    public bool IsAtVenue { get; set; }

    /// <summary>D-174 (gap doc G11, Mockup page 26) — addressee chosen
    /// from the live-stream pills. Defaults to
    /// <see cref="SessionQuestionRecipient.Speaker"/> for clients on
    /// the pre-D-174 wire shape.</summary>
    public SessionQuestionRecipient Recipient { get; set; } = SessionQuestionRecipient.Speaker;
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
    SessionQuestionRecipient Recipient,
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
