// Tests: SIMF.Api.Tests/AdminAttendeesExportTests.cs,
//        SIMF.Api.Tests/GridContractTests.cs (the roster page's column keys are
//        joined against the declaration below, so a page/service disagreement
//        fails the build instead of 400ing a live grid)
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
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
///
/// <para>
/// The filter / search / sort half of that path is the shared grid seam
/// (<c>ApplyGrid</c>) rather than a hand-written block: the paged query IS a
/// single Identity <c>IQueryable</c>, so the seam can own everything up to the
/// ordering. It stops there. <c>ToGridPageAsync</c> is deliberately NOT used,
/// because it also owns the paging and the projection, and neither fits here —
/// the row merges two databases so the projection cannot be one expression, and
/// the export deliberately pages past <c>MaxTop</c> to <see cref="ExportRowCap"/>.
/// </para>
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

    /// <summary>
    /// The grid contract for /admin/attendees: one entry per key the roster page
    /// and its export can send, as both its filter and its sort. A key that is
    /// neither declared here nor resolved in <see cref="BuildFilteredUsersAsync"/>
    /// is a 400, not a silently ignored request.
    ///
    /// <para>
    /// Every column is a <c>SimfUser</c> (Identity DB) column. Nothing here
    /// touches the App-DB profile, which is what keeps the whole grid one
    /// <c>IQueryable</c> — and it is also why none of the AES-GCM encrypted
    /// columns (<c>UserProfile.MobileNumber</c> / <c>SaudiMobile</c> /
    /// <c>InternationalMobile</c>) can be reached from here at all.
    /// </para>
    /// </summary>
    private static readonly GridColumns<SimfUser> Columns = new GridColumns<SimfUser>()
        .Add("email", user => user.Email, searchable: true)
        .Add("displayName", user => user.DisplayName, searchable: true)
        .Add("userType", user => user.UserType)
        .Add("accountState", user => user.AccountState)
        .Add("createdAt", user => user.CreatedAt)
        // The registration range is a pair of half-open BOUNDS, not the calendar
        // day a DateTime column would otherwise infer. The page sends "to" as
        // "<date>T23:59:59" so the picked day is inclusive; reading that as a day
        // would truncate it back to midnight and silently drop the last day.
        .AddFilter("from", raw =>
        {
            var earliest = ParseMoment("from", raw);
            return user => user.CreatedAt >= earliest;
        })
        .AddFilter("to", raw =>
        {
            var latest = ParseMoment("to", raw);
            return user => user.CreatedAt <= latest;
        })
        // Newest first, matching the hand-written natural order this replaces.
        .DefaultOrder("createdAt", descending: true)
        .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // The page-size policy is declared once, beside the columns; this reads
        // it back rather than repeating the two numbers.
        var (skip, top) = query.ClampPage(Columns.FallbackTop, Columns.MaxTop);

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

    // Identity-side filtered + sorted query (admins excluded). The two keys the
    // seam cannot own are resolved here first; everything else is the seam's.
    private async Task<IQueryable<SimfUser>> BuildFilteredUsersAsync(
        GridQuery query, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking()
            .Where(user => user.UserType != UserType.Admin);

        // A COPY of the caller's filters. Both ListAsync and ExportAsync are
        // handed the request's own GridQuery, so the keys consumed below are
        // removed from the copy and never from the request itself.
        var filters = new Dictionary<string, string>(
            query.Filters, StringComparer.OrdinalIgnoreCase);

        // profileTypeId lives on the OTHER database (UserProfile, App DB), so it
        // is resolved into an id set first and composed onto the source as a
        // scope predicate, ahead of every predicate the seam builds. The two
        // contexts are physically separate databases, so the join that would let
        // the seam own this key does not exist to be written.
        if (filters.Remove("profileTypeId", out var profileTypeFilter)
            && Guid.TryParse(profileTypeFilter, out var profileTypeId))
        {
            var matchingUserIds = await appDbContext.UserProfiles
                .AsNoTracking()
                .Where(profile => profile.ProfileTypeId == profileTypeId)
                .Select(profile => profile.UserId)
                .ToListAsync(cancellationToken);
            users = users.Where(user => matchingUserIds.Contains(user.Id));
        }

        // The roster page's UserType dropdown offers "Other", which stopped being
        // a UserType member when the enum collapsed to (Visitor, Admin), and an
        // empty "All". Neither has narrowed anything since, and handing either to
        // the seam would turn a shipped dropdown into a 400 on a live page. Admin
        // is dropped for the reason it was always ignored: the roster excludes
        // admins outright, so filtering to them can only return nothing.
        if (filters.TryGetValue("userType", out var userTypeFilter)
            && !NarrowsTheRoster(userTypeFilter))
        {
            filters.Remove("userType");
        }

        return users.ApplyGrid(WithFilters(query, filters), Columns, user => user.Id);
    }

    // True only when the value names a UserType the roster can actually narrow
    // to. The NAME is checked first, deliberately: the seam parses enum filters
    // by name only, so a numeric form kept here would be rejected there.
    private static bool NarrowsTheRoster(string raw) =>
        Enum.GetNames<UserType>().Any(name =>
            string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
        && !string.Equals(raw, nameof(UserType.Admin), StringComparison.OrdinalIgnoreCase);

    // The request as the seam may see it: everything the caller sent, over the
    // pruned filter set.
    private static GridQuery WithFilters(
        GridQuery query, Dictionary<string, string> filters) =>
        new()
        {
            Skip = query.Skip,
            Top = query.Top,
            Search = query.Search,
            Sort = query.Sort,
            SortDescending = query.SortDescending,
            Filters = filters,
        };

    // A registration-range bound. Invariant culture and the FULL value, not the
    // calendar day: see the "from" / "to" declarations above.
    private static DateTime ParseMoment(string key, string raw) =>
        DateTime.TryParse(
            raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? moment
            : throw GridFilters.ValueInvalid(key, raw, "a date such as 2026-08-16");

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
