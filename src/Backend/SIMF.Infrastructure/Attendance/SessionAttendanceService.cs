// Tests: SIMF.Api.Tests/SessionAttendanceTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Attendance.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Attendance;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Attendance;

/// <summary>
/// FR-506 — computes the Control Panel session-attendance dashboard from the
/// existing <c>HallAttendance</c> arrival records (D-241). Pure reads: every
/// query is <c>AsNoTracking</c> and nothing is written, so there is no schema
/// change and no migration. The live-now count rides the
/// <c>(HallId, LeaveUtc)</c> index and the per-session counts ride
/// <c>(SessionId, UserId)</c> (both on <c>HallAttendanceConfiguration</c>).
///
/// <para>All data is in the App database; <c>UserId</c> is counted as an opaque
/// Guid and never resolved against the Identity database (D-157 — no cross-DB
/// join). "Distinct attendees" is computed as one row per distinct
/// <c>(SessionId, UserId)</c> pair so the same person re-entering a hall
/// (a new row after a departure closed the prior one) counts once.</para>
/// </summary>
internal sealed class SessionAttendanceService(
    SimfAppDbContext appDbContext) : ISessionAttendanceService
{
    public async Task<SessionAttendanceSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        // Live now = distinct people currently inside a hall (an OPEN row).
        var liveAttendeesNow = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.LeaveUtc == null)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Active sessions that have at least one arrival.
        var sessionsWithAttendance = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => appDbContext.Sessions.Any(s => s.Id == a.SessionId && s.IsActive))
            .Select(a => a.SessionId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Total arrivals = distinct (session, attendee) pairs across active
        // sessions (i.e. the sum of every active session's distinct-attendee
        // count).
        var totalArrivals = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => appDbContext.Sessions.Any(s => s.Id == a.SessionId && s.IsActive))
            .Select(a => new { a.SessionId, a.UserId })
            .Distinct()
            .CountAsync(cancellationToken);

        return new SessionAttendanceSummary(
            liveAttendeesNow, sessionsWithAttendance, totalArrivals);
    }

    public async Task<GridPage<SessionAttendanceRow>> ListSessionAttendanceAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 20, 1, 200);

        var sessions = appDbContext.Sessions.AsNoTracking()
            .Where(session => session.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            sessions = sessions.Where(session =>
                EF.Functions.Like(session.Title, $"%{term}%")
                || EF.Functions.Like(session.TitleArabic, $"%{term}%")
                || EF.Functions.Like(session.Code, $"%{term}%"));
        }

        // Per-column grid filters (SimfDataGrid) — Contains() per mapped column.
        foreach (var filter in query.Filters)
        {
            var value = filter.Value;
            if (string.IsNullOrWhiteSpace(value)) { continue; }

            sessions = filter.Key.ToLowerInvariant() switch
            {
                "title" => sessions.Where(session => session.Title.Contains(value)),
                "code" => sessions.Where(session => session.Code.Contains(value)),
                _ => sessions,
            };
        }

        sessions = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => sessions.OrderByDescending(session => session.Code),
            ("code", false) => sessions.OrderBy(session => session.Code),
            ("title", true) => sessions.OrderByDescending(session => session.Title),
            ("title", false) => sessions.OrderBy(session => session.Title),
            ("startutc", true) => sessions.OrderByDescending(session => session.StartUtc),
            _ => sessions.OrderBy(session => session.StartUtc),
        };

        var total = await sessions.CountAsync(cancellationToken);

        var pageSessions = await sessions.Skip(skip).Take(top)
            .Select(session => new
            {
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                HallName = session.Hall!.Name,
                HallNameArabic = session.Hall!.NameArabic,
                session.StartUtc,
                session.EndUtc,
            })
            .ToListAsync(cancellationToken);

        var ids = pageSessions.Select(session => session.Id).ToList();

        // Distinct attendees per session for the page: one DB row per distinct
        // (session, attendee) pair (GROUP BY SessionId, UserId), then count per
        // session in memory. Kept to the page's ids so it never scans the whole
        // table. (COUNT(DISTINCT) is intentionally avoided for portable EF SQL.)
        var distinctPairs = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.SessionId))
            .GroupBy(a => new { a.SessionId, a.UserId })
            .Select(g => g.Key.SessionId)
            .ToListAsync(cancellationToken);
        var totalBySession = distinctPairs
            .GroupBy(sessionId => sessionId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Live-now per session for the page (open rows only).
        var liveRows = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.SessionId) && a.LeaveUtc == null)
            .GroupBy(a => a.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var liveBySession = liveRows.ToDictionary(row => row.SessionId, row => row.Count);

        var rows = pageSessions.Select(session => new SessionAttendanceRow(
            session.Id,
            session.Code,
            session.Title,
            session.TitleArabic,
            session.HallName,
            session.HallNameArabic,
            session.StartUtc,
            session.EndUtc,
            totalBySession.GetValueOrDefault(session.Id),
            liveBySession.GetValueOrDefault(session.Id))).ToList();

        return GridPage<SessionAttendanceRow>.Of(rows, total,
            new GridQuery { Skip = skip, Top = top });
    }
}
