using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>P5.1 — D-241 (FDS-003 §5.4): the attendee's device reports its
/// current GPS position to claim arrival at a session's hall. The server checks
/// the point against the hall's geofence (D-240) and records a
/// <see cref="AttendanceMethod.Geofence"/> arrival. The raw coordinates are NOT
/// stored — only the derived enter/leave times (FDS-003 §10, sensitive PII).</summary>
public sealed class RecordArrivalRequest
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

/// <summary>P5.1 — D-241: the attendee's current attendance state for a session.
/// <see cref="Arrived"/> is true while an open attendance row exists (entered,
/// not yet left). Returned by the arrival, departure, and status endpoints.</summary>
public sealed record HallAttendanceStatus(
    bool Arrived,
    DateTimeOffset? EnterUtc,
    DateTimeOffset? LeaveUtc,
    AttendanceMethod? Method);
