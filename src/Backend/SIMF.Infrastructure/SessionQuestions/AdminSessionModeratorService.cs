// Tests: SIMF.Api.Tests/AdminSessionModeratorsTests.cs
// Tests: SIMF.Api.Tests/SessionModeratorEligibilityTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Profiles;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// Admin: assign / revoke per-session moderator
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
    /// <summary>
    /// The grid contract for /admin/session-moderators. The moderator + assigner
    /// names live in the Identity database and are resolved after the page is
    /// chosen, so they are neither sortable nor filterable — no cross-database JOIN
    /// exists to make them either.
    /// </summary>
    private static readonly GridColumns<SessionModerator> Columns =
        new GridColumns<SessionModerator>()
            // One key that the page sends BOTH as a sort and as a filter, over three
            // columns, so it has to be one declaration rather than a hand-written
            // filter (which could not be sorted). Ordering by the concatenation is
            // the code-then-title order the page had; matching against it is the
            // same three-way OR, since a term inside any part is inside the whole.
            .Add("session", grant =>
                grant.Session!.Code + " " + grant.Session.Title + " " + grant.Session.TitleArabic,
                searchable: true)
            .Add("assignedAt", grant => grant.AssignedAt)
            // Not a column on the page: the single-session filter the endpoint
            // accepted before this list moved onto the shared grid.
            .Add("sessionId", grant => grant.SessionId)
            .DefaultOrder("assignedAt", descending: true)
            .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<AdminSessionModeratorRow>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // The session is reached through the grant's required navigation, which is
        // the same INNER JOIN the hand-written query spelled out, so the grid can
        // filter and sort on the session's own columns.
        // The tiebreak is the moderator, not the whole (SessionId, UserId) key: a
        // composite cannot be expressed as one ordering key without converting both
        // GUIDs to text per row, and the moderator alone already separates every tie
        // either order can produce — same session, or the same person assigned twice
        // in the same tick.
        var page = await appDbContext.SessionModerators
            .ToGridPageAsync(
                query, Columns, grant => grant.UserId,
                grant => new
                {
                    grant.SessionId,
                    grant.UserId,
                    grant.AssignedByUserId,
                    grant.AssignedAt,
                    Code = grant.Session!.Code,
                    Title = grant.Session.Title,
                    TitleArabic = grant.Session.TitleArabic,
                },
                cancellationToken);

        var pageRows = page.Items;
        if (pageRows.Count == 0)
        {
            return GridPage<AdminSessionModeratorRow>.Of(
                Array.Empty<AdminSessionModeratorRow>(), page.Total,
                page.Skip, page.Top);
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

        return GridPage<AdminSessionModeratorRow>.Of(
            items, page.Total, page.Skip, page.Top);
    }

    public async Task<SessionModeratorAssignOptions> ListAssignOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        // The session picker: the active sessions, code-ordered
        // (the same shape the CP's live-hall picker uses).
        var sessions = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .Select(s => new SessionModeratorSessionOption(
                s.Id, s.Code, s.Title, s.TitleArabic))
            .ToListAsync(cancellationToken);

        // The moderator picker: the App-side eligibility fact
        // (profile type carries MobileAppRole.Moderator) intersected with the
        // Identity-side approval fact. Two queries against two databases, joined
        // in memory — never a cross-database JOIN.
        // A moderator signs in to run the desk, so an attendee with no account is
        // not a candidate however eligible the profile type makes them.
        var eligibleRows = await EligibleProfiles()
            .Where(p => p.UserId != null)
            .Select(p => new
            {
                UserId = p.UserId!.Value,
                ProfileTypeName = p.ProfileType!.Name,
                ProfileTypeNameArabic = p.ProfileType.NameArabic,
            })
            .ToListAsync(cancellationToken);
        var candidates = new List<SessionModeratorCandidate>();
        if (eligibleRows.Count > 0)
        {
            // A user has at most one profile row; fold defensively so a stray
            // duplicate cannot throw on the picker.
            var byUserId = eligibleRows
                .GroupBy(r => r.UserId)
                .ToDictionary(g => g.Key, g => g.First());
            var eligibleIds = byUserId.Keys.ToList();
            var users = await identityDbContext.Users.AsNoTracking()
                .Where(u => eligibleIds.Contains(u.Id)
                    && u.AccountState == AccountState.Approved)
                .Select(u => new { u.Id, u.Email, u.DisplayName })
                .ToListAsync(cancellationToken);
            candidates = users
                .Select(u => new SessionModeratorCandidate(
                    u.Id,
                    u.DisplayName,
                    u.Email,
                    byUserId[u.Id].ProfileTypeName,
                    byUserId[u.Id].ProfileTypeNameArabic))
                .OrderBy(c => c.DisplayName)
                .ToList();
        }

        return new SessionModeratorAssignOptions(sessions, candidates);
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

        // "Approved" alone would let ANY visitor be handed a moderation
        // desk by a typo in the old free-text GUID box. The account must also be
        // ELIGIBLE: an assigned partner profile type carrying
        // MobileAppRole.Moderator — the same fact
        // UserProfileService.ResolveMobileAppRoleAsync mints into the JWT, so the
        // grant can never outrun the app role that opens the desk.
        var isEligible = await EligibleProfiles()
            .AnyAsync(p => p.UserId == request.UserId, cancellationToken);
        if (!isEligible)
        {
            throw new ApiException(
                ErrorCodes.SessionModeratorNotEligible, 400,
                "The account is not eligible to moderate — assign it a profile type whose mobile app role is Moderator first.",
                "الحساب غير مؤهل للمحاورة — عيّن له نوع ملف شخصي دوره في التطبيق \"محاوِر\" أولاً.");
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

        var now = timeProvider.SimfNow();
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

    // -- helpers ---------------------------------------------------------------

    /// <summary>The App-side eligibility fact: the account has an
    /// assigned PARTNER profile type (<c>IsForVisitor = false</c>) whose
    /// <c>MobileAppRole</c> is <see cref="MobileAppRole.Moderator"/>. That is
    /// exactly the condition <c>UserProfileService.ResolveMobileAppRoleAsync</c>
    /// uses to mint the app's moderator role, so the picker and the server-side
    /// assign check share one rule. The Identity-side approval fact is applied
    /// separately by the caller — the two databases are never joined.
    /// Returned UNPROJECTED so the assign path can test ONE user
    /// (<c>AnyAsync</c> over the filter) instead of materialising every eligible
    /// profile — EF cannot translate an <c>Any</c> applied on top of a projection
    /// into a custom type, so the projection stays with the picker caller.</summary>
    private IQueryable<UserProfile> EligibleProfiles() =>
        appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.ProfileType != null
                && !p.ProfileType.IsForVisitor
                && p.ProfileType.MobileAppRole == MobileAppRole.Moderator);
}
