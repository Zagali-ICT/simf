// Tests: SIMF.Api.Tests/SessionQuestionCommitteeTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// P3.3 — D-212 (Completion Programme §5.3): the Scientific-Committee central
/// Q&amp;A queue (stage 2). Trusts the caller is authorized — the endpoint layer
/// gates with Questions.View / Moderate / Escalate. Cross-DB submitter display
/// names are resolved against the Identity DB (no cross-DB JOIN, D-157).
/// </summary>
internal sealed class SessionQuestionCommitteeService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionQuestionCommitteeService> logger) : ISessionQuestionCommitteeService
{
    public async Task<IReadOnlyList<SessionQuestionQueueRow>> ListQueueAsync(
        QuestionStatus? status, Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        var wanted = status ?? QuestionStatus.Pending;
        var query = appDbContext.SessionQuestions
            .AsNoTracking()
            .Where(q => q.Status == wanted);
        if (sessionId is { } sid)
        {
            query = query.Where(q => q.SessionId == sid);
        }

        var rows = await query
            .OrderBy(q => q.CreatedAt)
            // Bounded triage view (oldest-first). The full queue can grow large on
            // a busy event; the Committee works the head of the queue and rows
            // re-appear as they are actioned. (A GridPage page-through is a
            // deferred enhancement if the team wants it.)
            .Take(200)
            .Select(q => new
            {
                q.Id,
                q.SessionId,
                SessionTitle = q.Session!.Title,
                q.SubmittedByUserId,
                q.QuestionText,
                q.Recipient,
                q.Phase,
                q.Status,
                q.AiFilterVerdict,
                q.AssignedToRole,
                q.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SessionQuestionQueueRow>();
        }

        var userIds = rows.Select(r => r.SubmittedByUserId).Distinct().ToList();
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(r =>
        {
            users.TryGetValue(r.SubmittedByUserId, out var user);
            return new SessionQuestionQueueRow(
                r.Id,
                r.SessionId,
                r.SessionTitle,
                r.SubmittedByUserId,
                user?.DisplayName ?? string.Empty,
                user?.Email,
                r.QuestionText,
                r.Recipient,
                r.Phase,
                r.Status,
                r.AiFilterVerdict,
                r.AssignedToRole,
                r.CreatedAt);
        }).ToList();
    }

    public async Task<SessionQuestionQueueRow> ApproveAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await LoadAsync(questionId, cancellationToken);
        if (question.Status != QuestionStatus.Approved)
        {
            question.Status = QuestionStatus.Approved;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.SessionQuestionApproved, actorUserId, question, cancellationToken);
            logger.LogInformation(
                "Committee {Actor} approved question {QuestionId}", actorUserId, questionId);
        }
        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionQueueRow> HideAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await LoadAsync(questionId, cancellationToken);
        if (question.Status != QuestionStatus.Hidden)
        {
            // Status is the single source of truth for visibility; the desk's
            // IsHidden marker is derived from it at projection time, so there is
            // no separate flag to keep in sync.
            question.Status = QuestionStatus.Hidden;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.SessionQuestionHidden, actorUserId, question, cancellationToken);
            logger.LogInformation(
                "Committee {Actor} hid question {QuestionId}", actorUserId, questionId);
        }
        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionQueueRow> EscalateAsync(
        Guid actorUserId, Guid questionId, string role,
        CancellationToken cancellationToken = default)
    {
        var trimmedRole = (role ?? string.Empty).Trim();
        if (trimmedRole.Length is < 1 or > 64)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "The escalation role must be between 1 and 64 characters.",
                "يجب أن يتراوح طول دور التصعيد بين 1 و 64 حرفاً.");
        }

        var question = await LoadAsync(questionId, cancellationToken);
        question.AssignedToRole = trimmedRole;
        question.EscalatedByUserId = actorUserId;
        question.EscalatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            AuditEvents.SessionQuestionEscalated, actorUserId, question,
            cancellationToken, detailSuffix: $"; role={trimmedRole}");
        logger.LogInformation(
            "Committee {Actor} escalated question {QuestionId} to role {Role}",
            actorUserId, questionId, trimmedRole);

        return await ToRowAsync(question, cancellationToken);
    }

    // -- helpers ---------------------------------------------------------------

    // No Include(q => q.Session): we mutate only the question and read just the
    // title — fetched as a scalar in ToRowAsync — so the wide Session row
    // (Description/recording columns) is never loaded or tracked.
    private async Task<SessionQuestion> LoadAsync(Guid questionId, CancellationToken cancellationToken) =>
        await appDbContext.SessionQuestions
            .SingleOrDefaultAsync(q => q.Id == questionId, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionQuestionNotFound, 404,
            "The question was not found.",
            "لم يتم العثور على السؤال.");

    private Task AuditAsync(
        string eventType, Guid actorUserId, SessionQuestion question,
        CancellationToken cancellationToken, string detailSuffix = "") =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            // The affected submitter is the audit subject (mirrors AdminSessionModeratorService).
            SubjectUserId = question.SubmittedByUserId,
            Detail = $"questionId={question.Id}; sessionId={question.SessionId}{detailSuffix}",
        }, cancellationToken);

    // Mirror of SessionModerationService.ToRowAsync — submitter resolved from
    // the Identity DB (no cross-DB JOIN, D-157); title is a separate scalar read.
    private async Task<SessionQuestionQueueRow> ToRowAsync(
        SessionQuestion question, CancellationToken cancellationToken)
    {
        var title = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == question.SessionId)
            .Select(s => s.Title)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var user = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == question.SubmittedByUserId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        return new SessionQuestionQueueRow(
            question.Id,
            question.SessionId,
            title,
            question.SubmittedByUserId,
            user?.DisplayName ?? string.Empty,
            user?.Email,
            question.QuestionText,
            question.Recipient,
            question.Phase,
            question.Status,
            question.AiFilterVerdict,
            question.AssignedToRole,
            question.CreatedAt);
    }
}
