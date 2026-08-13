// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Reporting;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Attendance report — one row per session, with how many distinct people
/// arrived and how many are still inside. Sourced entirely from the App
/// database (Session / Hall / HallAttendance), so no cross-context query.
///
/// <para><c>Session</c> has no <c>HallAttendances</c> navigation (arrivals are
/// their own aggregate), so the per-session counts are correlated subqueries
/// over the <c>HallAttendances</c> set rather than navigation counts.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<AttendanceReportRow>> GetAttendanceAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterSessions(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortSessions(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(s => new SessionAttendanceProjection(
                s.Id,
                s.Code,
                s.Title,
                s.Hall != null ? s.Hall.Name : string.Empty,
                s.Start,
                appDbContext.HallAttendances
                    .Where(a => a.SessionId == s.Id)
                    .Select(a => a.UserProfileId)
                    .Distinct()
                    .Count(),
                appDbContext.HallAttendances
                    .Count(a => a.SessionId == s.Id && a.Leave == null)))
            .ToListAsync(cancellationToken);

        // Totals describe the whole filtered set, not this page — a header figure
        // that changed when you turned the page would be worse than none.
        var sessionIds = filtered.Select(s => s.Id);

        var distinctAttendees = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => a.UserProfileId)
            .Distinct()
            .CountAsync(cancellationToken);

        var insideNow = await appDbContext.HallAttendances.AsNoTracking()
            .CountAsync(
                a => sessionIds.Contains(a.SessionId) && a.Leave == null,
                cancellationToken);

        return new ReportPage<AttendanceReportRow>(
            page.Select(ToRow).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Sessions", Figure(total)),
                new ReportTotal("Admin.Reports.Total.DistinctAttendees", Figure(distinctAttendees)),
                new ReportTotal("Admin.Reports.Total.LiveNow", Figure(insideNow)),
            ]);
    }

    public async Task<byte[]> ExportAttendanceAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortSessions(FilterSessions(query), query)
            .Take(ExportRowCap)
            .Select(s => new SessionAttendanceProjection(
                s.Id,
                s.Code,
                s.Title,
                s.Hall != null ? s.Hall.Name : string.Empty,
                s.Start,
                appDbContext.HallAttendances
                    .Where(a => a.SessionId == s.Id)
                    .Select(a => a.UserProfileId)
                    .Distinct()
                    .Count(),
                appDbContext.HallAttendances
                    .Count(a => a.SessionId == s.Id && a.Leave == null)))
            .ToListAsync(cancellationToken);

        return Export(
            rows.Select(ToRow).ToList(),
            [
                new GridExcelColumn<AttendanceReportRow>("Code", r => r.Code),
                new GridExcelColumn<AttendanceReportRow>("Session", r => r.Title),
                new GridExcelColumn<AttendanceReportRow>("Hall", r => r.HallName),
                new GridExcelColumn<AttendanceReportRow>("Start", r => r.StartDisplay),
                new GridExcelColumn<AttendanceReportRow>("Attendees", r => r.Attendees),
                new GridExcelColumn<AttendanceReportRow>("Inside now", r => r.LiveNow),
            ],
            "attendance");
    }

    /// <summary>The shape the SQL projection returns: the raw instant, because
    /// the Saudi display string cannot be built inside a translated query.</summary>
    private sealed record SessionAttendanceProjection(
        Guid Id,
        string Code,
        string Title,
        string HallName,
        DateTime Start,
        int Attendees,
        int LiveNow);

    private static AttendanceReportRow ToRow(SessionAttendanceProjection p) =>
        new(p.Id, p.Code, p.Title, p.HallName, p.Start.FormatSaudi(), p.Attendees, p.LiveNow);

    /// <summary>
    /// Active sessions whose start falls inside the requested Saudi window, with
    /// the free-text term applied to the code, both titles and the hall name.
    /// </summary>
    private IQueryable<Session> FilterSessions(ReportQuery query)
    {
        var window = ResolveWindow(query);
        var sessions = appDbContext.Sessions.AsNoTracking().Where(s => s.IsActive);

        if (window.Start is { } start)
        {
            sessions = sessions.Where(s => s.Start >= start);
        }

        if (window.End is { } end)
        {
            sessions = sessions.Where(s => s.Start < end);
        }

        if (SearchTerm(query) is { } term)
        {
            sessions = sessions.Where(s =>
                s.Code.Contains(term)
                || s.Title.Contains(term)
                || s.TitleArabic.Contains(term)
                || (s.Hall != null && s.Hall.Name.Contains(term)));
        }

        return sessions;
    }

    /// <summary>
    /// Applies the requested sort. An unknown key falls back to start time rather
    /// than throwing, and every ordering ends on Id so paging stays stable when
    /// two sessions share a sort value.
    /// </summary>
    private static IQueryable<Session> SortSessions(
        IQueryable<Session> sessions, ReportQuery query)
    {
        var descending = query.Grid.SortDescending;

        return query.Grid.Sort switch
        {
            "code" => descending
                ? sessions.OrderByDescending(s => s.Code).ThenBy(s => s.Id)
                : sessions.OrderBy(s => s.Code).ThenBy(s => s.Id),
            "title" => descending
                ? sessions.OrderByDescending(s => s.Title).ThenBy(s => s.Id)
                : sessions.OrderBy(s => s.Title).ThenBy(s => s.Id),
            "hall" => descending
                ? sessions.OrderByDescending(s => s.Hall!.Name).ThenBy(s => s.Id)
                : sessions.OrderBy(s => s.Hall!.Name).ThenBy(s => s.Id),
            _ => descending
                ? sessions.OrderByDescending(s => s.Start).ThenBy(s => s.Id)
                : sessions.OrderBy(s => s.Start).ThenBy(s => s.Id),
        };
    }
}
