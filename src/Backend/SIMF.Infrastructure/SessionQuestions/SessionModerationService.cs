// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
// Tests: SIMF.Api.Tests/SessionQuestionCommitteeTests.cs (P3.3 — D-234 desk = Approved set)
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
/// D-169 (gap doc G6) — moderator surface. Trusts the caller is
/// already authorized; the endpoint layer handles the role check.
/// </summary>
internal sealed class SessionModerationService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionModerationService> logger) : ISessionModerationService
{
    public async Task<IReadOnlyList<SessionQuestionModeratorRow>> ListAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // P3.3 — D-212: the desk shows the Committee-approved set only (stage 3).
        // Pending questions await the Committee (stage 2); Hidden ones were
        // rejected. Recovery of a hidden question is via the Committee queue
        // (its status=Hidden filter), not this desk.
        var rows = await appDbContext.SessionQuestions
            .AsNoTracking()
            .Where(q => q.SessionId == sessionId && q.Status == QuestionStatus.Approved)
            .OrderBy(q => q.Order).ThenBy(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.SessionId,
                q.SubmittedByUserId,
                q.QuestionText,
                q.Recipient,
                q.Order,
                q.IsPushed,
                q.PushedAt,
                q.CreatedAt,
                q.Phase,
                q.Status,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SessionQuestionModeratorRow>();
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
            return new SessionQuestionModeratorRow(
                r.Id,
                r.SessionId,
                r.SubmittedByUserId,
                user?.DisplayName ?? string.Empty,
                user?.Email,
                r.QuestionText,
                r.Recipient,
                r.Order,
                r.Status == QuestionStatus.Hidden,
                r.IsPushed,
                r.PushedAt,
                r.CreatedAt,
                r.Phase,
                r.Status);
        }).ToList();
    }

    public async Task<SessionQuestionModeratorRow> SetHiddenAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isHidden,
        CancellationToken cancellationToken = default)
    {
        var question = await LoadQuestionAsync(sessionId, questionId, cancellationToken);

        // P3.3 — D-212: Status is the single source of truth for visibility; the
        // row's IsHidden marker is derived from it. Hide → Hidden, un-hide →
        // Approved. (The persisted IsHidden column is no longer written.)
        var currentlyHidden = question.Status == QuestionStatus.Hidden;
        if (currentlyHidden == isHidden)
        {
            return await ToRowAsync(question, cancellationToken); // idempotent
        }
        question.Status = isHidden ? QuestionStatus.Hidden : QuestionStatus.Approved;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = isHidden
                ? AuditEvents.SessionQuestionHidden
                : AuditEvents.SessionQuestionUnhidden,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; questionId={questionId}",
        }, cancellationToken);

        logger.LogInformation(
            "Moderator {Actor} {Action} question {QuestionId} on session {SessionId}",
            actorUserId, isHidden ? "hid" : "unhid", questionId, sessionId);

        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionModeratorRow> PushAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var question = await LoadQuestionAsync(sessionId, questionId, cancellationToken);

        if (question.IsPushed)
        {
            return await ToRowAsync(question, cancellationToken); // idempotent
        }
        question.IsPushed = true;
        question.PushedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionQuestionPushed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; questionId={questionId}",
        }, cancellationToken);

        return await ToRowAsync(question, cancellationToken);
    }

    public async Task ReorderAsync(
        Guid actorUserId,
        Guid sessionId,
        IReadOnlyList<Guid> orderedQuestionIds,
        CancellationToken cancellationToken = default)
    {
        if (orderedQuestionIds.Count == 0)
        {
            return;
        }
        var distinctIds = orderedQuestionIds.Distinct().ToList();
        if (distinctIds.Count != orderedQuestionIds.Count)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Reorder list contains duplicate ids.",
                "تحتوي قائمة الترتيب على معرّفات مكررة.");
        }

        // Reorder is a full-list contract — the supplied set must
        // cover every question on the session, hidden included.
        // Partial-list reorder would assign 0..n-1 over a subset and
        // collide with unlisted rows' existing Order values.
        var allOnSession = await appDbContext.SessionQuestions
            .Where(q => q.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        var allIds = allOnSession.Select(q => q.Id).ToHashSet();
        var supplied = distinctIds.ToHashSet();
        if (!supplied.SetEquals(allIds))
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Reorder list must contain every question on the session exactly once.",
                "يجب أن تشمل قائمة الترتيب جميع أسئلة الجلسة بالضبط مرة واحدة.");
        }

        var trackedById = allOnSession.ToDictionary(q => q.Id);
        for (var i = 0; i < distinctIds.Count; i++)
        {
            trackedById[distinctIds[i]].Order = i;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionQuestionReordered,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; count={distinctIds.Count}",
        }, cancellationToken);
    }

    public Task<bool> IsModeratorAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        appDbContext.SessionModerators
            .AsNoTracking()
            .AnyAsync(m => m.SessionId == sessionId && m.UserId == userId,
                cancellationToken);

    // -- helpers ---------------------------------------------------------------

    private async Task<SessionQuestion> LoadQuestionAsync(
        Guid sessionId, Guid questionId, CancellationToken cancellationToken)
    {
        var question = await appDbContext.SessionQuestions
            .SingleOrDefaultAsync(q => q.SessionId == sessionId && q.Id == questionId,
                cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionQuestionNotFound, 404,
                "The question was not found on this session.",
                "لم يتم العثور على السؤال على هذه الجلسة.");
        return question;
    }

    private async Task<SessionQuestionModeratorRow> ToRowAsync(
        SessionQuestion question, CancellationToken cancellationToken)
    {
        var user = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == question.SubmittedByUserId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        return new SessionQuestionModeratorRow(
            question.Id,
            question.SessionId,
            question.SubmittedByUserId,
            user?.DisplayName ?? string.Empty,
            user?.Email,
            question.QuestionText,
            question.Recipient,
            question.Order,
            question.Status == QuestionStatus.Hidden,
            question.IsPushed,
            question.PushedAt,
            question.CreatedAt,
            question.Phase,
            question.Status);
    }
}
