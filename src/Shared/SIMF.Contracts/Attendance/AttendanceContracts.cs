namespace SIMF.Contracts.Attendance;

/// <summary>
/// FR-506 (SIMF-SRS-001 §3.5; SIMF-FDS-003 §5.5) — the live top-line of the
/// Control Panel session-attendance dashboard. A read-only aggregate computed
/// on demand over the existing <c>HallAttendance</c> arrival records (D-241);
/// no schema, no writes. Served by <c>GET /api/v1/admin/attendance/summary</c>.
///
/// <para><c>LiveAttendeesNow</c> is the distinct count of people currently
/// inside any hall (an open attendance row — <c>LeaveUtc</c> is null).
/// <c>SessionsWithAttendance</c> is the number of active sessions that have at
/// least one arrival. <c>TotalArrivals</c> is the sum over active sessions of
/// each session's distinct-attendee count.</para>
/// </summary>
public sealed record SessionAttendanceSummary(
    int LiveAttendeesNow,
    int SessionsWithAttendance,
    int TotalArrivals);

/// <summary>
/// FR-506 — one row of the per-session attendance grid, served (server-paged)
/// by <c>POST /api/v1/admin/attendance/sessions/list</c> as a
/// <c>GridPage&lt;SessionAttendanceRow&gt;</c>. Active sessions only.
///
/// <para><c>TotalAttendees</c> is the distinct count of people who arrived at
/// the session's hall — any <c>HallAttendance</c> enter record, whether by GPS
/// geofence (D-241) or operator QR door-scan (D-244). <c>LiveNow</c> is the
/// count currently inside (open rows). The attendee identity itself is never
/// resolved here — <c>UserId</c> is counted as an opaque Guid (D-157, no
/// cross-DB join into the Identity database).</para>
/// </summary>
public sealed record SessionAttendanceRow(
    Guid SessionId,
    string Code,
    string Title,
    string TitleArabic,
    string HallName,
    string HallNameArabic,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int TotalAttendees,
    int LiveNow);
