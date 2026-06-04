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
        Guid userId,
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
                c.LikeCount,
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

        // B5 — D-223: which of these comments the requester has liked, in one
        // round-trip (avoids an N+1 over the feed).
        var commentIds = rows.Select(r => r.Id).ToList();
        var likedByMe = (await appDbContext.SessionCommentLikes
            .AsNoTracking()
            .Where(like => like.UserId == userId && commentIds.Contains(like.CommentId))
            .Select(like => like.CommentId)
            .ToListAsync(cancellationToken)).ToHashSet();

        return rows.Select(r =>
        {
            users.TryGetValue(r.UserId, out var user);
            return new SessionCommentFeedRow(
                r.Id,
                r.SessionId,
                r.UserId,
                user?.DisplayName ?? string.Empty,
                r.Body,
                r.CreatedAt,
                r.LikeCount,
                likedByMe.Contains(r.Id));
        }).ToList();
    }

    public async Task<SessionCommentLikeResult> LikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await LoadLikeableCommentAsync(commentId, cancellationToken);

        var existing = await appDbContext.SessionCommentLikes
            .FindAsync([commentId, userId], cancellationToken);
        if (existing is not null)
        {
            // Idempotent — already liked. No state change, no audit.
            return new SessionCommentLikeResult(commentId, comment.LikeCount, true);
        }

        appDbContext.SessionCommentLikes.Add(new SessionCommentLike
        {
            CommentId = commentId,
            UserId = userId,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        comment.LikeCount += 1;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCommentLiked,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"commentId={commentId}; likeCount={comment.LikeCount}",
        }, cancellationToken);

        return new SessionCommentLikeResult(commentId, comment.LikeCount, true);
    }

    public async Task<SessionCommentLikeResult> UnlikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await LoadLikeableCommentAsync(commentId, cancellationToken);

        var existing = await appDbContext.SessionCommentLikes
            .FindAsync([commentId, userId], cancellationToken);
        if (existing is null)
        {
            // Idempotent — not liked. No state change, no audit.
            return new SessionCommentLikeResult(commentId, comment.LikeCount, false);
        }

        appDbContext.SessionCommentLikes.Remove(existing);
        comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCommentUnliked,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"commentId={commentId}; likeCount={comment.LikeCount}",
        }, cancellationToken);

        return new SessionCommentLikeResult(commentId, comment.LikeCount, false);
    }

    // B5 — D-223: the tracked comment a like/unlike targets. Only an Approved
    // + active comment is likeable; anything else is a 404 (a hidden / deleted
    // comment is not visible to the audience, so it cannot be liked).
    private async Task<SessionComment> LoadLikeableCommentAsync(
        Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await appDbContext.SessionComments
            .SingleOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null
            || !comment.IsActive
            || comment.Status != SessionCommentStatus.Approved)
        {
            throw new ApiException(
                ErrorCodes.SessionCommentNotFound, 404,
                "The comment was not found.",
                "لم يتم العثور على التعليق.");
        }
        return comment;
    }
}
