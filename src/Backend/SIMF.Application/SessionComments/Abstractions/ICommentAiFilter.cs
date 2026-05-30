using SIMF.Common.Enums;

namespace SIMF.Application.SessionComments.Abstractions;

/// <summary>
/// D-199 — the AI-moderation seam for audience comments. On every
/// comment submission the service calls <see cref="ScreenAsync"/> to
/// decide the landing <see cref="SessionCommentStatus"/> (Approved →
/// visible immediately; Pending → held for a human moderator).
///
/// <para><b>Stub in this increment (D-199):</b> the shipped
/// implementation is a deterministic stub that does not call any AI
/// provider — it returns a configurable default verdict. The real
/// moderation call (routing through the central <c>IAiService</c> /
/// the <c>question-filter</c>-style prompt catalogue, gap doc G12 /
/// D-176) is wired later behind THIS interface, so the comment service
/// and its tests do not change when it lands. This is the explicit
/// "clear seam for the real AI filter" the increment requires.</para>
/// </summary>
public interface ICommentAiFilter
{
    /// <summary>Screen one comment body. Returns the landing status the
    /// comment should take and a short free-text verdict bucket (e.g.
    /// <c>approved</c>, <c>flagged</c>, <c>stub-default</c>) persisted
    /// on the row for audit.</summary>
    Task<CommentFilterVerdict> ScreenAsync(
        Guid sessionId,
        Guid userId,
        string body,
        CancellationToken cancellationToken = default);
}

/// <summary>D-199 — the AI filter's decision. <see cref="Status"/> is
/// the landing moderation state (only Approved or Pending — the filter
/// never auto-hides; a definite block is still surfaced to a human as
/// Pending so the decision is auditable). <see cref="Verdict"/> is the
/// short bucket persisted to <c>SessionComment.AiFilterVerdict</c>.</summary>
public sealed record CommentFilterVerdict(
    SessionCommentStatus Status,
    string Verdict);
