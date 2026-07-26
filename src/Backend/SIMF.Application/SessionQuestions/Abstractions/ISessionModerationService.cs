using SIMF.Common;
using SIMF.Common.Enums;
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
    /// <summary>Returns the moderator queue for one session, ordered by
    /// <c>Order</c> then <c>CreatedAt</c>.
    /// <para>DEF-MOD-002 — <paramref name="status"/> selects the desk tab:
    /// <c>null</c> (the default) returns the WORKING desk — the Committee-approved
    /// set plus the rows the moderator has already marked
    /// <see cref="QuestionStatus.Answered"/>; an explicit status returns exactly
    /// that bucket, which is how the desk retrieves its own rejected
    /// (<see cref="QuestionStatus.Hidden"/>) rows so a mis-click is recoverable.
    /// The caller is already authorized as a moderator of this session, so a
    /// hidden row never leaks to an attendee — attendees have no route to
    /// this surface.</para></summary>
    Task<IReadOnlyList<SessionQuestionModeratorRow>> ListAsync(
        Guid sessionId,
        QuestionStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Hide or unhide one question (idempotent).</summary>
    Task<SessionQuestionModeratorRow> SetHiddenAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isHidden,
        CancellationToken cancellationToken = default);

    /// <summary>DEF-MOD-001 — mark / unmark one question as ANSWERED on stage
    /// (idempotent). Only an <see cref="QuestionStatus.Approved"/> question can be
    /// marked answered; un-marking returns it to
    /// <see cref="QuestionStatus.Approved"/>.</summary>
    Task<SessionQuestionModeratorRow> SetAnsweredAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isAnswered,
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
