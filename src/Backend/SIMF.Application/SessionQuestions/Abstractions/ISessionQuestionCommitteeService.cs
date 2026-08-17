using SIMF.Common;
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
    /// <summary>One server-paged page of the queue, oldest question first.
    /// The <c>status</c> and <c>sessionId</c> the queue used to take as
    /// parameters are now declared grid filter keys, so the Control Panel sends
    /// them the same way it sends every other column. A request that names no
    /// <c>status</c> gets the default Pending bucket, which is what the
    /// parameterless call returned.</summary>
    Task<GridPage<SessionQuestionQueueRow>> ListQueueAsync(
        GridQuery query, CancellationToken cancellationToken = default);

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
