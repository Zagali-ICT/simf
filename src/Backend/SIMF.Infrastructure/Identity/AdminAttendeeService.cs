// Tests: SIMF.Api.Tests/AdminAttendeesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-134 Sprint A — read-only attendee roster. Joins
/// <c>SimfUser</c> + <c>UserProfile</c> + <c>ProfileType</c>; admins are
/// excluded by default (they're not event attendees). **AsNoTracking,
/// no schema change.**
/// </summary>
internal sealed class AdminAttendeeService(SimfIdentityDbContext dbContext)
    : IAdminAttendeeService
{
    public async Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        // Join via a left-join from SimfUser → UserProfile → ProfileType.
        // Admins are excluded — they're not event attendees. The left-join
        // means a Visitor / Other who hasn't filled their profile yet still
        // appears in the roster (with null profile-type cells), which is
        // the intended behaviour for the desk operator.
        var rows =
            from user in dbContext.Users.AsNoTracking()
            where user.UserType != UserType.Admin
            join profile in dbContext.UserProfiles.AsNoTracking()
                on user.Id equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            join profileType in dbContext.ProfileTypes.AsNoTracking()
                on profile != null ? profile.ProfileTypeId : (Guid?)null
                equals profileType.Id into profileTypeJoin
            from profileType in profileTypeJoin.DefaultIfEmpty()
            select new
            {
                user,
                profile,
                profileType,
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(row =>
                EF.Functions.Like(row.user.Email!, $"%{term}%")
                || EF.Functions.Like(row.user.DisplayName, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("userType", out var userTypeFilter)
            && !string.IsNullOrWhiteSpace(userTypeFilter)
            && !string.Equals(userTypeFilter, "All", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<UserType>(userTypeFilter, ignoreCase: true, out var userTypeValue)
            && userTypeValue != UserType.Admin)
        {
            rows = rows.Where(row => row.user.UserType == userTypeValue);
        }
        if (query.Filters.TryGetValue("profileTypeId", out var profileTypeFilter)
            && Guid.TryParse(profileTypeFilter, out var profileTypeId))
        {
            rows = rows.Where(row => row.profile != null
                && row.profile.ProfileTypeId == profileTypeId);
        }
        if (query.Filters.TryGetValue("accountState", out var stateFilter)
            && !string.IsNullOrWhiteSpace(stateFilter)
            && Enum.TryParse<AccountState>(stateFilter, ignoreCase: true, out var state))
        {
            rows = rows.Where(row => row.user.AccountState == state);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("email", true) => rows.OrderByDescending(row => row.user.Email),
            ("email", false) => rows.OrderBy(row => row.user.Email),
            ("displayname", true) => rows.OrderByDescending(row => row.user.DisplayName),
            ("displayname", false) => rows.OrderBy(row => row.user.DisplayName),
            ("usertype", true) => rows.OrderByDescending(row => row.user.UserType),
            ("usertype", false) => rows.OrderBy(row => row.user.UserType),
            ("createdat", false) => rows.OrderBy(row => row.user.CreatedAt),
            _ => rows.OrderByDescending(row => row.user.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(row => new AdminAttendeeSummary(
                row.user.Id,
                row.user.Email ?? string.Empty,
                row.user.DisplayName,
                row.user.UserType.ToString(),
                row.profile != null ? row.profile.ProfileTypeId : null,
                row.profileType != null ? row.profileType.Name : null,
                row.profileType != null ? row.profileType.NameArabic : null,
                row.user.AccountState.ToString(),
                row.profile != null ? row.profile.QrId : null,
                row.user.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminAttendeeSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }
}
