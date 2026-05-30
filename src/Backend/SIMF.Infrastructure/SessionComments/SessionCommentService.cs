// Tests: SIMF.Api.Tests/SessionCommentsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionComments.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SessionComments;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionComments;

/// <summary>
/// D-199 (Mockup page 28 — "Audience comments") — public submission +
/// approved-feed service. Mirrors <c>SessionQuestionService</c>:
/// validate body, validate the session exists + is active, audit, log.
/// The moderation landing state is decided by the
/// <see cref="ICommentAiFilter"/> seam (a stub in this increment).
/// </summary>
internal sealed class SessionCommentService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    ICommentAiFilter aiFilter,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionCommentService> logger) : ISessionCommentService
{
    public async Task<SessionCommentSubmitted> SubmitAsync(
        Guid sessionId,
        Guid userId,
        SubmitSessionCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = (request.Body ?? string.Empty).Trim();
        if (body.Length is < 1 or > 1000)
        {
            throw new ApiException(
                ErrorCodes.SessionCommentInvalid, 400,
                "Comment text must be between 1 and 1000 characters.",
                "يجب أن يتراوح طول نص التعليق بين 1 و 1000 حرف.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.IsActive, s.Code })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        if (!session.IsActive)
        {
            throw new ApiException(
                ErrorCodes.SessionNotOpenForComments, 400,
                "The session is not accepting comments.",
                "الجلسة لا تستقبل التعليقات.");
        }

        // D-199 — AI-filter seam (stub now). Decides the landing
        // moderation state. The filter never throws on normal input;
        // it returns Approved or Pending plus a verdict bucket.
        var verdict = await aiFilter.ScreenAsync(sessionId, userId, body, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var comment = new SessionComment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            Body = body,
            Status = verdict.Status,
            AiFilterVerdict = verdict.Verdict,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.SessionComments.Add(comment);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCommentSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"sessionId={sessionId}; commentId={comment.Id}; "
                + $"status={comment.Status}; aiVerdict={verdict.Verdict}",
        }, cancellationToken);

        logger.LogInformation(
            "Audience comment {CommentId} submitted on session {SessionId} ({Code}) "
                + "by {UserId} landing {Status} (AI verdict {Verdict})",
            comment.Id, sessionId, session.Code, userId, comment.Status, verdict.Verdict);

        return new SessionCommentSubmitted(
            comment.Id, sessionId, comment.Status, comment.CreatedAt);
    }

    public async Task<IReadOnlyList<SessionCommentFeedRow>> ListApprovedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.SessionComments
            .AsNoTracking()
            .Where(c => c.SessionId == sessionId
                && c.IsActive
                && c.Status == SessionCommentStatus.Approved)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.SessionId,
                c.UserId,
                c.Body,
                c.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SessionCommentFeedRow>();
        }

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(r =>
        {
            users.TryGetValue(r.UserId, out var user);
            return new SessionCommentFeedRow(
                r.Id,
                r.SessionId,
                r.UserId,
                user?.DisplayName ?? string.Empty,
                r.Body,
                r.CreatedAt);
        }).ToList();
    }
}
