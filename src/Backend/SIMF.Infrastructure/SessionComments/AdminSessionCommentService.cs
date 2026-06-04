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
/// D-199 (Mockup page 28) — admin moderation over audience comments.
/// Mirrors AdminSpeakerService (Skip/Top paging via GridQuery + audit)
/// + SessionModerationService (idempotent status transitions + cross-DB
/// submitter projection). Trusts the caller is already authorized
/// (Administrator) — the endpoint layer enforces the role.
/// </summary>
internal sealed class AdminSessionCommentService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSessionCommentService> logger) : IAdminSessionCommentService
{
    public async Task<GridPage<SessionCommentModerationRow>> ListAsync(
        Guid sessionId,
        SessionCommentStatus? status,
        GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var comments = appDbContext.SessionComments
            .AsNoTracking()
            .Where(c => c.SessionId == sessionId && c.IsActive);

        if (status is not null)
        {
            comments = comments.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            comments = comments.Where(c => EF.Functions.Like(c.Body, $"%{term}%"));
        }

        // Default ordering: newest first (the moderation queue works the
        // most-recent comments). Sort can override to oldest-first.
        comments = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("created", false) => comments.OrderBy(c => c.CreatedAt),
            _ => comments.OrderByDescending(c => c.CreatedAt),
        };

        var total = await comments.CountAsync(cancellationToken);

        var page = await comments
            .Skip(skip)
            .Take(top)
            .Select(c => new
            {
                c.Id,
                c.SessionId,
                c.UserId,
                c.Body,
                c.Status,
                c.AiFilterVerdict,
                c.CreatedAt,
                c.ModeratedByUserId,
                c.ModeratedAt,
            })
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            return GridPage<SessionCommentModerationRow>.Of(
                Array.Empty<SessionCommentModerationRow>(), total,
                new GridQuery { Skip = skip, Top = top });
        }

        var userIds = page.Select(r => r.UserId).Distinct().ToList();
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var rows = page.Select(r =>
        {
            users.TryGetValue(r.UserId, out var user);
            return new SessionCommentModerationRow(
                r.Id,
                r.SessionId,
                r.UserId,
                user?.DisplayName ?? string.Empty,
                user?.Email,
                r.Body,
                r.Status,
                r.AiFilterVerdict,
                r.CreatedAt,
                r.ModeratedByUserId,
                r.ModeratedAt);
        }).ToList();

        return GridPage<SessionCommentModerationRow>.Of(rows, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<SessionCommentModerationRow> SetStatusAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid commentId,
        SessionCommentStatus status,
        CancellationToken cancellationToken = default)
    {
        var comment = await LoadAsync(sessionId, commentId, cancellationToken);

        if (comment.Status == status)
        {
            return await ToRowAsync(comment, cancellationToken); // idempotent
        }

        comment.Status = status;
        comment.ModeratedByUserId = actorUserId;
        comment.ModeratedAt = timeProvider.GetUtcNow();
        comment.UpdatedAt = comment.ModeratedAt;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = status switch
            {
                SessionCommentStatus.Approved => AuditEvents.SessionCommentApproved,
                SessionCommentStatus.Hidden => AuditEvents.SessionCommentHidden,
                _ => AuditEvents.SessionCommentRepended,
            },
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; commentId={commentId}; status={status}",
        }, cancellationToken);

        logger.LogInformation(
            "Moderator {Actor} set comment {CommentId} on session {SessionId} to {Status}",
            actorUserId, commentId, sessionId, status);

        return await ToRowAsync(comment, cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = await LoadAsync(sessionId, commentId, cancellationToken);

        comment.Deactivate();
        comment.ModeratedByUserId = actorUserId;
        comment.ModeratedAt = timeProvider.GetUtcNow();
        comment.UpdatedAt = comment.ModeratedAt;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCommentDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; commentId={commentId}",
        }, cancellationToken);
    }

    // -- helpers ---------------------------------------------------------------

    private async Task<SessionComment> LoadAsync(
        Guid sessionId, Guid commentId, CancellationToken cancellationToken)
    {
        return await appDbContext.SessionComments
            .SingleOrDefaultAsync(
                c => c.SessionId == sessionId && c.Id == commentId && c.IsActive,
                cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionCommentNotFound, 404,
                "The comment was not found on this session.",
                "لم يتم العثور على التعليق على هذه الجلسة.");
    }

    private async Task<SessionCommentModerationRow> ToRowAsync(
        SessionComment comment, CancellationToken cancellationToken)
    {
        var user = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == comment.UserId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        return new SessionCommentModerationRow(
            comment.Id,
            comment.SessionId,
            comment.UserId,
            user?.DisplayName ?? string.Empty,
            user?.Email,
            comment.Body,
            comment.Status,
            comment.AiFilterVerdict,
            comment.CreatedAt,
            comment.ModeratedByUserId,
            comment.ModeratedAt);
    }
}
