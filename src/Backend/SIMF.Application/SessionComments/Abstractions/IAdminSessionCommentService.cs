using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.SessionComments.Abstractions;

/// <summary>
/// D-199 (Mockup page 28) — admin moderation surface for audience
/// comments. Distinct from the public submission/feed service
/// (<see cref="ISessionCommentService"/>). The endpoint layer enforces
/// the Administrator role; this service trusts that and does the work.
/// </summary>
public interface IAdminSessionCommentService
{
    /// <summary>Paged moderation list across a session's comments,
    /// optionally filtered by <paramref name="status"/> (e.g. show only
    /// Pending for the moderation queue). Excludes soft-deleted rows.</summary>
    Task<GridPage<SessionCommentModerationRow>> ListAsync(
        Guid sessionId,
        SessionCommentStatus? status,
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Set a comment's moderation status (approve / hide /
    /// re-pend). Idempotent — re-setting the same status is a no-op.</summary>
    Task<SessionCommentModerationRow> SetStatusAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid commentId,
        SessionCommentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a comment (CLAUDE.md §7 — sets
    /// <c>IsActive = false</c>; the row is retained for audit).</summary>
    Task DeactivateAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}
