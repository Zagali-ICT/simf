using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.SessionQuestions.Abstractions;

/// <summary>The Scientific-Committee
/// central Q&amp;A queue — stage 2 of the pipeline (AI advisory → Committee →
/// per-session moderator). The Committee filters every question (pre + live)
/// before the per-session moderator desk sees the approved set. "Escalate to a
/// team" routes to a role (not an individual), leaving the question Pending for
/// that role to action.</summary>
public interface ISessionQuestionCommitteeService
{
    /// <summary>The queue, newest-question-first. <paramref name="status"/> null
    /// = the default Pending queue; <paramref name="sessionId"/> null = across
    /// all sessions.</summary>
    Task<IReadOnlyList<SessionQuestionQueueRow>> ListQueueAsync(
        QuestionStatus? status, Guid? sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Approve a question — it joins the per-session moderator desk's
    /// set. Idempotent when already Approved.</summary>
    Task<SessionQuestionQueueRow> ApproveAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default);

    /// <summary>Hide a question — never displayed (retained for audit).
    /// Idempotent when already Hidden.</summary>
    Task<SessionQuestionQueueRow> HideAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default);

    /// <summary>Escalate a question to a role/team. Sets the routing fields and
    /// leaves it Pending for that role to approve/hide.</summary>
    Task<SessionQuestionQueueRow> EscalateAsync(
        Guid actorUserId, Guid questionId, string role,
        CancellationToken cancellationToken = default);
}
