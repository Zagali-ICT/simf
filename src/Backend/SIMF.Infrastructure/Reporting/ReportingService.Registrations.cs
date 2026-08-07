// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Reporting;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Registrations report — one row per attendee account, filtered by the Saudi
/// day the account was created.
///
/// <para>This is the one report that genuinely spans both databases: the account
/// lives in Identity (<c>SimfUser</c>) and its profile type lives in App
/// (<c>UserProfile</c> -> <c>UserProfileType</c>). A cross-database join is
/// forbidden, so the page of accounts is read from Identity first and the profile-type
/// names for exactly those user ids are then resolved with a second query against
/// App. Nothing is duplicated and no constraint spans the two.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<RegistrationReportRow>> GetRegistrationsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterRegistrations(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortRegistrations(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(u => new RegistrationProjection(
                u.Id,
                u.DisplayName,
                u.Email ?? string.Empty,
                u.AccountState,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        var approved = await filtered
            .CountAsync(u => u.AccountState == AccountState.Approved, cancellationToken);
        var pending = await filtered
            .CountAsync(u => u.AccountState == AccountState.PendingApproval, cancellationToken);

        var profileTypes = await ResolveProfileTypeNamesAsync(
            page.Select(p => p.UserId).ToList(), cancellationToken);

        return new ReportPage<RegistrationReportRow>(
            page.Select(p => ToRow(p, profileTypes)).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Registrations", Figure(total)),
                new ReportTotal("Admin.Reports.Total.Approved", Figure(approved)),
                new ReportTotal("Admin.Reports.Total.Pending", Figure(pending)),
            ]);
    }

    public async Task<byte[]> ExportRegistrationsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortRegistrations(FilterRegistrations(query), query)
            .Take(ExportRowCap)
            .Select(u => new RegistrationProjection(
                u.Id,
                u.DisplayName,
                u.Email ?? string.Empty,
                u.AccountState,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        var profileTypes = await ResolveProfileTypeNamesAsync(
            rows.Select(r => r.UserId).ToList(), cancellationToken);

        return Export(
            rows.Select(r => ToRow(r, profileTypes)).ToList(),
            [
                new GridExcelColumn<RegistrationReportRow>("Name", r => r.DisplayName),
                new GridExcelColumn<RegistrationReportRow>("Email", r => r.Email),
                new GridExcelColumn<RegistrationReportRow>("Profile type", r => r.ProfileTypeName),
                new GridExcelColumn<RegistrationReportRow>("Account state", r => r.AccountState),
                new GridExcelColumn<RegistrationReportRow>("Registered", r => r.RegisteredDisplay),
            ],
            "registrations");
    }

    /// <summary>
    /// Profile-type display names for the given Identity user ids, read from the
    /// App database in one query. Users with no profile row, or a profile with no
    /// type assigned, are simply absent from the result and render as blank
    /// rather than as a guess.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolveProfileTypeNamesAsync(
        IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pairs = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId)
                && p.ProfileType != null
                && p.ProfileType.Name != string.Empty)
            .Select(p => new { p.UserId, Name = p.ProfileType!.Name })
            .ToListAsync(cancellationToken);

        // A user could in principle carry more than one profile row; take the
        // first deterministically rather than throwing on a duplicate key.
        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }

    private sealed record RegistrationProjection(
        Guid UserId,
        string DisplayName,
        string Email,
        AccountState AccountState,
        DateTime CreatedAt);

    private static RegistrationReportRow ToRow(
        RegistrationProjection p, Dictionary<Guid, string> profileTypes) =>
        new(
            p.UserId,
            p.DisplayName,
            p.Email,
            profileTypes.TryGetValue(p.UserId, out var name) ? name : string.Empty,
            p.AccountState.ToString(),
            p.CreatedAt.FormatSaudi());

    /// <summary>
    /// Attendee (non-admin) accounts created inside the requested Saudi window.
    /// Admin accounts are excluded: this reports who registered for the forum,
    /// not who operates it.
    /// </summary>
    private IQueryable<SimfUser> FilterRegistrations(ReportQuery query)
    {
        var window = ResolveWindow(query);
        var users = identityDbContext.Users.AsNoTracking()
            .Where(u => u.UserType == UserType.Visitor);

        if (window.Start is { } start)
        {
            users = users.Where(u => u.CreatedAt >= start);
        }

        if (window.End is { } end)
        {
            users = users.Where(u => u.CreatedAt < end);
        }

        if (SearchTerm(query) is { } term)
        {
            users = users.Where(u =>
                u.DisplayName.Contains(term)
                || (u.Email != null && u.Email.Contains(term)));
        }

        return users;
    }

    private static IQueryable<SimfUser> SortRegistrations(
        IQueryable<SimfUser> users, ReportQuery query)
    {
        var descending = query.Grid.SortDescending;

        return query.Grid.Sort switch
        {
            "name" => descending
                ? users.OrderByDescending(u => u.DisplayName).ThenBy(u => u.Id)
                : users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id),
            "email" => descending
                ? users.OrderByDescending(u => u.Email).ThenBy(u => u.Id)
                : users.OrderBy(u => u.Email).ThenBy(u => u.Id),
            "state" => descending
                ? users.OrderByDescending(u => u.AccountState).ThenBy(u => u.Id)
                : users.OrderBy(u => u.AccountState).ThenBy(u => u.Id),
            // "registered" is the date column's own Key (RegistrationsReport
            // .razor:63). Without this arm a click on it fell through to the
            // default below, which reads `descending` INVERTED so that the
            // no-sort case comes back newest-first. The grid therefore drew an
            // ascending arrow — and rendered aria-sort="ascending" — over
            // newest-first rows, and a descending arrow over oldest-first ones.
            // The default still wants that inversion; an explicit click must
            // not inherit it.
            "registered" => descending
                ? users.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
                : users.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            // Newest first by default: a registrations report is normally read
            // to see who has just signed up.
            _ => descending
                ? users.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
                : users.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id),
        };
    }
}
