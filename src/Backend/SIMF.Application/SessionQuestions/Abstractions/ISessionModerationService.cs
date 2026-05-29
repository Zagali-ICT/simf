using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.SessionQuestions.Abstractions;

/// <summary>
/// D-169 (gap doc G6) — moderator surface for one session's question
/// queue. Authorization is layered: the endpoint checks the
/// <c>SessionModerator</c> table for (sessionId, callerUserId)
/// existence (or Administrator-role bypass); the service trusts the
/// caller is already authorized.
/// </summary>
public interface ISessionModerationService
{
    /// <summary>Returns the moderator queue for one session — all
    /// rows including hidden + pushed, ordered by <c>Order</c>.</summary>
    Task<IReadOnlyList<SessionQuestionModeratorRow>> ListAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Hide or unhide one question (idempotent).</summary>
    Task<SessionQuestionModeratorRow> SetHiddenAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isHidden,
        CancellationToken cancellationToken = default);

    /// <summary>Mark a question as pushed to the speaker (one-way).
    /// Idempotent — re-pushing leaves the timestamp from the first push.</summary>
    Task<SessionQuestionModeratorRow> PushAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        CancellationToken cancellationToken = default);

    /// <summary>Replace the queue order with the supplied list. The
    /// service replays the <c>Order</c> column 0..n-1 in the order
    /// of the supplied ids. Any question id not in the list keeps
    /// its existing Order; the service does not append.</summary>
    Task ReorderAsync(
        Guid actorUserId,
        Guid sessionId,
        IReadOnlyList<Guid> orderedQuestionIds,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true when the supplied user is a moderator
    /// for the supplied session (used by the authorization handler).
    /// Administrator-role callers are NOT short-circuited here — the
    /// caller bypasses by checking the role separately.</summary>
    Task<bool> IsModeratorAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
