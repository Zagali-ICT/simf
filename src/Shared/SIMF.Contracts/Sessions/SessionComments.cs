using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>D-199 (Mockup page 28 — "Audience comments") — audience
/// submission request body for
/// <c>POST /api/v1/app/sessions/{sessionId}/comments</c>. The endpoint's
/// own route shape merges this with the SessionId route param.</summary>
public sealed class SubmitSessionCommentRequest
{
    /// <summary>The comment text. Trimmed; 1–1000 chars.</summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>D-199 — public-facing response after submission. Echoes the
/// landing <see cref="Status"/> the AI filter assigned so the client
/// can show the right pill immediately (Approved → visible;
/// Pending → "awaiting review").</summary>
public sealed record SessionCommentSubmitted(
    Guid Id,
    Guid SessionId,
    SessionCommentStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>D-199 — one row on the public audience comments feed
/// (Mockup page 28). Carries the author's display name + a stable
/// per-user avatar key (initials are derived client-side, as the
/// mockup does). Body is always an Approved + active comment.</summary>
public sealed record SessionCommentFeedRow(
    Guid Id,
    Guid SessionId,
    Guid UserId,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAt,
    // B5 — D-223: appended (additive — wire contract preserved, D-219).
    int LikeCount,
    bool LikedByMe);

/// <summary>B5 — D-223: result of a like / unlike action. Echoes the comment's
/// new <see cref="LikeCount"/> and the caller's resulting
/// <see cref="LikedByMe"/> state so the client updates without a refetch.</summary>
public sealed record SessionCommentLikeResult(
    Guid CommentId,
    int LikeCount,
    bool LikedByMe);

/// <summary>D-199 — one row in the admin moderation list. Adds the
/// moderation fields the public feed omits: current
/// <see cref="Status"/>, the AI filter verdict, and the author's email
/// (PII — admin surface only), projected from the cross-DB lookup so
/// the moderator does not issue a second request.</summary>
public sealed record SessionCommentModerationRow(
    Guid Id,
    Guid SessionId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorEmail,
    string Body,
    SessionCommentStatus Status,
    string? AiFilterVerdict,
    DateTimeOffset CreatedAt,
    Guid? ModeratedByUserId,
    DateTimeOffset? ModeratedAt);

/// <summary>D-199 — admin moderation action: set a comment's status
/// (approve / hide / re-pend). <see cref="Status"/> is the target
/// state (idempotent — re-setting the same state is a no-op success).</summary>
public sealed class SetSessionCommentStatusRequest
{
    public SessionCommentStatus Status { get; set; }
}
