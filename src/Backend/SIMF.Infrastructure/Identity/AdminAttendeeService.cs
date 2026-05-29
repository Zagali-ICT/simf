// Tests: SIMF.Api.Tests/AdminAttendeesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-134 Sprint A / D-167 — read-only attendee roster. After D-167 moved
/// <c>UserProfile</c> + <c>ProfileType</c> onto <c>SimfAppDbContext</c>,
/// the user + profile + profile-type join can no longer be a single SQL
/// query (the two DbContexts hit different physical databases). Pattern:
/// page the SimfUser rows (Identity DB), then load the matching
/// UserProfile + ProfileType rows (App DB) keyed by user id, then merge
/// in memory. Total = the Identity count; the App round-trip only fetches
/// the visible page's worth of rows.
/// </summary>
internal sealed class AdminAttendeeService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext)
    : IAdminAttendeeService
{
    public async Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        // 1) Identity-side page: SimfUser filtered + sorted, admins
        // excluded. profileTypeId-as-filter requires a cross-DB lookup;
        // we defer it to step 3 and apply in-memory.
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

        // If the caller filters by profile type, we have to evaluate
        // it across the two contexts: fetch matching UserProfile.UserId
        // values from App DB first and restrict the Identity query.
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

        var total = await users.CountAsync(cancellationToken);
        var pageUsers = await users
            .Skip(skip)
            .Take(top)
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

        // 2) App-side load for the visible page: UserProfile + ProfileType
        // keyed by user id.
        var userIds = pageUsers.Select(u => u.Id).ToList();
        var profilesByUserId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .Select(profile => new
            {
                profile.UserId,
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

        // 3) Merge in memory.
        var page = pageUsers.Select(user =>
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

        return GridPage<AdminAttendeeSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }
}
