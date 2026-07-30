using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.SessionQuestions.Abstractions;

/// <summary>
/// D-169 (gap doc G6) — admin surface for assigning + revoking
/// per-session moderator grants. Distinct from any in-app role; this
/// is a per-resource permission.
/// </summary>
public interface IAdminSessionModeratorService
{
    /// <summary>Lists every per-session moderator grant — joins
    /// Session metadata + moderator display name + email.
    /// Server-paged via <see cref="GridQuery"/>.</summary>
    Task<GridPage<AdminSessionModeratorRow>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>DEF-MOD-005 — the two option lists behind the assign dialog:
    /// the active sessions and the accounts ELIGIBLE to moderate. Replaces the
    /// two raw GUID text boxes, which offered no way to find the right person
    /// and handed a moderation desk to whoever a typo landed on.</summary>
    Task<SessionModeratorAssignOptions> ListAssignOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Assign a user as moderator of a specific session.
    /// Validates the session exists + is active, the user exists,
    /// is approved and is ELIGIBLE to moderate (DEF-MOD-005 — an
    /// assigned partner profile type carrying
    /// <c>MobileAppRole.Moderator</c>). Duplicate (sessionId, userId) raises
    /// <c>ErrorCodes.SessionModeratorAlreadyAssigned</c>.</summary>
    Task<AdminSessionModeratorRow> AssignAsync(
        Guid actorUserId,
        AssignSessionModeratorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Revoke a grant. Idempotent — a no-op if the grant
    /// does not exist.</summary>
    Task RevokeAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
