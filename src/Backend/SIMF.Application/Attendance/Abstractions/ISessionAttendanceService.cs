using SIMF.Common;
using SIMF.Contracts.Attendance;

namespace SIMF.Application.Attendance.Abstractions;

/// <summary>
/// FR-506 (SIMF-SRS-001 §3.5; SIMF-FDS-003 §5.5) — read-only session-attendance
/// reporting over the existing <c>HallAttendance</c> arrival records (D-241).
/// No schema, no writes — every method is an aggregate read. All data lives in
/// the App database; the attendee <c>UserId</c> is counted as an opaque Guid,
/// never resolved against the Identity database (D-157).
/// </summary>
public interface ISessionAttendanceService
{
    /// <summary>The live top-line: people currently inside any hall, the number
    /// of active sessions that have at least one arrival, and the total arrival
    /// count across active sessions.</summary>
    Task<SessionAttendanceSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One server-paged grid page of per-session attendance counts
    /// (active sessions only) — per-column filter on title / code, sort by
    /// start time (default), code or title.</summary>
    Task<GridPage<SessionAttendanceRow>> ListSessionAttendanceAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>2026-07-18 — the attendees currently present in a session's hall
    /// (open attendance rows), each with their App-DB profile data (name, org,
    /// profile type, job title) and seat, for the live per-session hall view.
    /// Ordered by arrival time. All reads are App-DB only (D-157 — the profile is
    /// resolved from <c>UserProfile</c>, never from the Identity database).</summary>
    Task<IReadOnlyList<SessionPresentAttendee>> GetPresentAttendeesAsync(
        Guid sessionId, CancellationToken cancellationToken = default);
}
