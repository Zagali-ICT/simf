// Tests: SIMF.Api.Tests/SessionAttendanceTests.cs, SIMF.Api.Tests/GridDateSortKeyTests.cs
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
/// <c>(HallId, Leave)</c> index and the per-session counts ride
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
            .Where(a => a.Leave == null)
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
        var (skip, top) = query.ClampPage(20, 200);

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
            // The key is "start", matching the grid column's Key (AttendanceDashboard
            // .razor) and the column itself. It read "startutc" until 2026-08-01, a
            // leftover from before D-770 renamed the persisted columns: nothing ever sent
            // that key, so clicking Start a second time silently fell through to the
            // ascending catch-all and the column could not be sorted newest-first at
            // all. The ascending arm is written out rather than left to `_` so the
            // pair is visible and the next rename cannot break only one half of it.
            ("start", true) => sessions.OrderByDescending(session => session.Start),
            ("start", false) => sessions.OrderBy(session => session.Start),
            _ => sessions.OrderBy(session => session.Start),
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
                session.Start,
                session.End,
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
            .Where(a => ids.Contains(a.SessionId) && a.Leave == null)
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
            session.Start,
            session.End,
            totalBySession.GetValueOrDefault(session.Id),
            liveBySession.GetValueOrDefault(session.Id))).ToList();

        return GridPage<SessionAttendanceRow>.Of(rows, total,
            skip, top);
    }

    public async Task<IReadOnlyList<SessionPresentAttendee>> GetPresentAttendeesAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Everyone currently inside this session's hall — the open attendance rows.
        var present = await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.Leave == null)
            .Select(a => new { a.UserId, a.Enter, a.Method })
            .ToListAsync(cancellationToken);
        if (present.Count == 0)
        {
            return Array.Empty<SessionPresentAttendee>();
        }

        var userIds = present.Select(a => a.UserId).Distinct().ToList();

        // Profile data — App DB only (D-157: resolved from UserProfile, never from
        // the Identity database). Admin-typed users carry no profile → null fields.
        var profiles = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                p.Name,
                p.NameArabic,
                p.JobTitle,
                OrganisationName = p.Organisation != null ? p.Organisation.Name : null,
                ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
            })
            .ToListAsync(cancellationToken);
        var profileByUser = profiles.ToDictionary(p => p.UserId);

        // Their still-held seat for this session (if any) — open-seating attendees
        // and admin blocks resolve to no row/seat.
        var seats = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null
                && r.ReservedForUserId != null && userIds.Contains(r.ReservedForUserId!.Value))
            .Select(r => new { UserId = r.ReservedForUserId!.Value, r.RowLabel, r.SeatNumber })
            .ToListAsync(cancellationToken);
        var seatByUser = seats
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        return present
            .OrderBy(a => a.Enter)
            .Select(a =>
            {
                var profile = profileByUser.GetValueOrDefault(a.UserId);
                var seat = seatByUser.GetValueOrDefault(a.UserId);
                return new SessionPresentAttendee(
                    a.UserId,
                    profile?.Name ?? string.Empty,
                    profile?.NameArabic ?? string.Empty,
                    profile?.OrganisationName,
                    profile?.ProfileTypeName,
                    profile?.JobTitle,
                    seat?.RowLabel,
                    seat?.SeatNumber,
                    a.Enter,
                    a.Method);
            })
            .ToList();
    }
}
