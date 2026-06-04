using SIMF.Contracts.Sessions;

namespace SIMF.Application.SessionComments.Abstractions;

/// <summary>
/// D-199 (Mockup page 28 — "Audience comments") — public-facing surface
/// for audience comment submission + the approved feed. Used by the
/// mobile app via <c>POST /api/v1/sessions/{sessionId}/comments</c> and
/// <c>GET /api/v1/sessions/{sessionId}/comments</c>.
/// </summary>
public interface ISessionCommentService
{
    /// <summary>Submit a comment to a session. Validates the session
    /// exists + is active, runs the body through the AI-filter seam to
    /// decide the landing status, persists the row, and returns its id
    /// + landing status.</summary>
    Task<SessionCommentSubmitted> SubmitAsync(
        Guid sessionId,
        Guid userId,
        SubmitSessionCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The public approved feed for a session — only
    /// Approved + active comments, newest first (Mockup page 28 lists
    /// most-recent at the top). <paramref name="userId"/> is the requesting
    /// attendee, used to project the per-row <c>LikedByMe</c> flag.</summary>
    Task<IReadOnlyList<SessionCommentFeedRow>> ListApprovedAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>B5 — D-223: like a comment (idempotent — liking again is a
    /// no-op that returns the current state). Only an Approved + active
    /// comment can be liked. Returns the new like count + LikedByMe=true.</summary>
    Task<SessionCommentLikeResult> LikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>B5 — D-223: remove the caller's like (idempotent — unliking
    /// when not liked is a no-op). Returns the new like count +
    /// LikedByMe=false.</summary>
    Task<SessionCommentLikeResult> UnlikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
