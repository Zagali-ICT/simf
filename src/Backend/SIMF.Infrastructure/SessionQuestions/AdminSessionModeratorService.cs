// Tests: SIMF.Api.Tests/AdminSessionModeratorsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// D-169 (gap doc G6) — admin: assign / revoke per-session moderator
/// grants. Cross-DB merge follows the same pattern as
/// <c>AdminAttendeeService</c>.
/// </summary>
internal sealed class AdminSessionModeratorService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSessionModeratorService> logger) : IAdminSessionModeratorService
{
    public async Task<GridPage<AdminSessionModeratorRow>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        // Join the session up front so the grid can filter / sort on the
        // session code/title (D-255). The moderator + assigner names live in
        // the Identity DB and are resolved on read below (D-157: no cross-DB
        // JOIN), so those columns are not server-filterable.
        var joined = appDbContext.SessionModerators.AsNoTracking()
            .Join(appDbContext.Sessions,
                g => g.SessionId, s => s.Id,
                (g, s) => new
                {
                    g.SessionId, g.UserId, g.AssignedByUserId, g.AssignedAt,
                    s.Code, s.Title, s.TitleArabic,
                });

        // Legacy single-session filter (kept for back-compat callers).
        if (query.Filters.TryGetValue("sessionId", out var sidRaw)
            && Guid.TryParse(sidRaw, out var sessionFilter))
        {
            joined = joined.Where(r => r.SessionId == sessionFilter);
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "session":
                    joined = joined.Where(r =>
                        r.Code.Contains(v)
                        || r.Title.Contains(v)
                        || r.TitleArabic.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: most-recently assigned.
        joined = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("session", false) => joined.OrderBy(r => r.Code).ThenBy(r => r.Title),
            ("session", true) => joined.OrderByDescending(r => r.Code).ThenByDescending(r => r.Title),
            ("assignedat", false) => joined.OrderBy(r => r.AssignedAt),
            ("assignedat", true) => joined.OrderByDescending(r => r.AssignedAt),
            _ => joined.OrderByDescending(r => r.AssignedAt),
        };

        var total = await joined.CountAsync(cancellationToken);
        var pageRows = await joined
            .Skip(skip)
            .Take(top)
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return GridPage<AdminSessionModeratorRow>.Of(
                Array.Empty<AdminSessionModeratorRow>(), total,
                new GridQuery { Skip = skip, Top = top });
        }

        var userIds = pageRows.Select(r => r.UserId)
            .Union(pageRows.Select(r => r.AssignedByUserId))
            .Distinct()
            .ToList();
        var users = await identityDbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = pageRows.Select(r =>
        {
            users.TryGetValue(r.UserId, out var moderator);
            users.TryGetValue(r.AssignedByUserId, out var assigner);
            return new AdminSessionModeratorRow(
                r.SessionId,
                r.Code,
                r.Title,
                r.TitleArabic,
                r.UserId,
                moderator?.DisplayName ?? string.Empty,
                moderator?.Email,
                r.AssignedByUserId,
                assigner?.DisplayName ?? string.Empty,
                r.AssignedAt);
        }).ToList();

        return GridPage<AdminSessionModeratorRow>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminSessionModeratorRow> AssignAsync(
        Guid actorUserId,
        AssignSessionModeratorRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == request.SessionId)
            .Select(s => new { s.Id, s.IsActive, s.Code, s.Title, s.TitleArabic })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        if (!session.IsActive)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Cannot assign a moderator to an inactive session.",
                "لا يمكن تعيين مشرف لجلسة غير مفعّلة.");
        }

        // Cross-DB existence + approval check against the Identity DB.
        var moderator = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.AccountState })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The moderator user was not found.",
                "لم يتم العثور على المستخدم المُشرف.");

        if (moderator.AccountState != AccountState.Approved)
        {
            throw new ApiException(
                ErrorCodes.AuthAccountNotApproved, 400,
                "Moderator must be an approved account.",
                "يجب أن يكون المُشرف حساباً معتمداً.");
        }

        var duplicate = await appDbContext.SessionModerators
            .AsNoTracking()
            .AnyAsync(m => m.SessionId == request.SessionId && m.UserId == request.UserId,
                cancellationToken);
        if (duplicate)
        {
            throw new ApiException(
                ErrorCodes.SessionModeratorAlreadyAssigned, 409,
                "This user is already a moderator of the session.",
                "هذا المستخدم مشرف على الجلسة بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var grant = new SessionModerator
        {
            SessionId = request.SessionId,
            UserId = request.UserId,
            AssignedByUserId = actorUserId,
            AssignedAt = now,
        };
        appDbContext.SessionModerators.Add(grant);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionModeratorAssigned,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = request.UserId,
            Detail = $"sessionId={request.SessionId}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {Actor} assigned {UserId} as moderator of session {SessionId}",
            actorUserId, request.UserId, request.SessionId);

        var assigner = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminSessionModeratorRow(
            session.Id, session.Code, session.Title, session.TitleArabic,
            moderator.Id, moderator.DisplayName, moderator.Email,
            actorUserId, assigner ?? string.Empty,
            now);
    }

    public async Task RevokeAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var grant = await appDbContext.SessionModerators
            .SingleOrDefaultAsync(m => m.SessionId == sessionId && m.UserId == userId,
                cancellationToken);
        if (grant is null)
        {
            return; // idempotent
        }
        appDbContext.SessionModerators.Remove(grant);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionModeratorRevoked,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = userId,
            Detail = $"sessionId={sessionId}",
        }, cancellationToken);
    }
}
