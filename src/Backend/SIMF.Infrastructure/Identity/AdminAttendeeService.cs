// Tests: SIMF.Api.Tests/AdminAttendeesTests.cs, SIMF.Api.Tests/AdminAttendeesExportTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Read-only attendee roster. Because <c>UserProfile</c> + <c>ProfileType</c>
/// live on <c>SimfAppDbContext</c>, the user + profile + profile-type join
/// cannot be a single SQL query (the two DbContexts hit different physical
/// databases). Pattern: page the SimfUser rows (Identity DB), then load the
/// matching UserProfile + ProfileType rows (App DB) keyed by user id, then merge
/// in memory. Total = the Identity count; the App round-trip only fetches
/// the visible page's worth of rows. The XLSX export and the CreatedAt
/// date-range filter share that one filter/merge path with the list.
/// </summary>
internal sealed class AdminAttendeeService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    IAttendeeExcelService excel)
    : IAdminAttendeeService
{
    /// <summary>The export bound. Matches the user export's cap so an
    /// accidental "export everything" can't load the whole roster into RAM.</summary>
    private const int ExportRowCap = 5_000;

    public async Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var users = await BuildFilteredUsersAsync(query, cancellationToken);

        var total = await users.CountAsync(cancellationToken);
        var page = await MaterialiseAsync(users, skip, top, cancellationToken);

        return GridPage<AdminAttendeeSummary>.Of(page, total,
            skip, top);
    }

    public async Task<byte[]> ExportAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var users = await BuildFilteredUsersAsync(query, cancellationToken);
        var rows = await MaterialiseAsync(users, 0, ExportRowCap, cancellationToken);

        var bytes = excel.Export(rows);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminAttendeesExported,
            actorUserId,
            $"count={rows.Count}",
            cancellationToken);

        return bytes;
    }

    // Identity-side filtered + sorted query (admins excluded). The
    // profileTypeId filter needs a cross-DB lookup, so this is async.
    private async Task<IQueryable<SimfUser>> BuildFilteredUsersAsync(
        GridQuery query, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking()
            .Where(user => user.UserType != UserType.Admin);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(user =>
                EF.Functions.Like(user.Email!, $"%{term}%")
                || EF.Functions.Like(user.DisplayName, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("userType", out var userTypeFilter)
            && !string.IsNullOrWhiteSpace(userTypeFilter)
            && !string.Equals(userTypeFilter, "All", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<UserType>(userTypeFilter, ignoreCase: true, out var userTypeValue)
            && userTypeValue != UserType.Admin)
        {
            users = users.Where(user => user.UserType == userTypeValue);
        }
        if (query.Filters.TryGetValue("accountState", out var stateFilter)
            && !string.IsNullOrWhiteSpace(stateFilter)
            && Enum.TryParse<AccountState>(stateFilter, ignoreCase: true, out var state))
        {
            users = users.Where(user => user.AccountState == state);
        }
        // Registration date-range filter (CreatedAt).
        if (query.Filters.TryGetValue("from", out var fromRaw)
            && DateTime.TryParse(fromRaw, out var from))
        {
            users = users.Where(user => user.CreatedAt >= from);
        }
        if (query.Filters.TryGetValue("to", out var toRaw)
            && DateTime.TryParse(toRaw, out var to))
        {
            users = users.Where(user => user.CreatedAt <= to);
        }

        users = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("email", true) => users.OrderByDescending(user => user.Email),
            ("email", false) => users.OrderBy(user => user.Email),
            ("displayname", true) => users.OrderByDescending(user => user.DisplayName),
            ("displayname", false) => users.OrderBy(user => user.DisplayName),
            ("usertype", true) => users.OrderByDescending(user => user.UserType),
            ("usertype", false) => users.OrderBy(user => user.UserType),
            ("createdat", false) => users.OrderBy(user => user.CreatedAt),
            _ => users.OrderByDescending(user => user.CreatedAt),
        };

        // If the caller filters by profile type, evaluate it across the two
        // contexts: fetch matching UserProfile.UserId values from App DB first
        // and restrict the Identity query.
        if (query.Filters.TryGetValue("profileTypeId", out var profileTypeFilter)
            && Guid.TryParse(profileTypeFilter, out var profileTypeId))
        {
            var matchingUserIds = await appDbContext.UserProfiles
                .AsNoTracking()
                .Where(profile => profile.ProfileTypeId == profileTypeId)
                .Select(profile => profile.UserId)
                .ToListAsync(cancellationToken);
            users = users.Where(user => matchingUserIds.Contains(user.Id));
        }

        return users;
    }

    // Page the Identity rows, then load + merge the App-side profile +
    // profile-type rows for just that page.
    private async Task<List<AdminAttendeeSummary>> MaterialiseAsync(
        IQueryable<SimfUser> users, int skip, int take, CancellationToken cancellationToken)
    {
        var pageUsers = await users
            .Skip(skip)
            .Take(take)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.UserType,
                user.AccountState,
                user.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var userIds = pageUsers.Select(u => u.Id).ToList();
        var profilesByUserId = await appDbContext.UserProfiles
            .AsNoTracking()
            // Keyed by account id because the page is a list of accounts; a
            // profile without one is not in `userIds` and cannot match.
            .Where(profile => profile.UserId != null
                && userIds.Contains(profile.UserId.Value))
            .Select(profile => new
            {
                UserId = profile.UserId!.Value,
                profile.ProfileTypeId,
                profile.QrId,
            })
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

        var profileTypeIds = profilesByUserId.Values
            .Where(profile => profile.ProfileTypeId.HasValue)
            .Select(profile => profile.ProfileTypeId!.Value)
            .Distinct()
            .ToList();
        var profileTypesById = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(profileType => profileTypeIds.Contains(profileType.Id))
            .Select(profileType => new
            {
                profileType.Id,
                profileType.Name,
                profileType.NameArabic,
            })
            .ToDictionaryAsync(profileType => profileType.Id, cancellationToken);

        return pageUsers.Select(user =>
        {
            profilesByUserId.TryGetValue(user.Id, out var profile);
            string? profileTypeName = null;
            string? profileTypeNameAr = null;
            Guid? profileTypeIdValue = null;
            if (profile?.ProfileTypeId is { } ptId
                && profileTypesById.TryGetValue(ptId, out var profileType))
            {
                profileTypeName = profileType.Name;
                profileTypeNameAr = profileType.NameArabic;
                profileTypeIdValue = ptId;
            }
            return new AdminAttendeeSummary(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.UserType.ToString(),
                profileTypeIdValue,
                profileTypeName,
                profileTypeNameAr,
                user.AccountState.ToString(),
                profile?.QrId,
                user.CreatedAt);
        }).ToList();
    }
}
