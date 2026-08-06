using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>The attendee's device reports its
/// current GPS position to claim arrival at a session's hall. The server checks
/// the point against the hall's geofence and records a
/// <see cref="AttendanceMethod.Geofence"/> arrival. The raw coordinates are NOT
/// stored — only the derived enter/leave times (sensitive PII).</summary>
public sealed class RecordArrivalRequest
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

/// <summary>The attendee's current attendance state for a session.
/// <see cref="Arrived"/> is true while an open attendance row exists (entered,
/// not yet left). Returned by the arrival, departure, and status endpoints.</summary>
public sealed record HallAttendanceStatus(
    bool Arrived,
    DateTime? Enter,
    DateTime? Leave,
    AttendanceMethod? Method);

/// <summary>The operator's hall-door QR scan — the badge QR id to
/// resolve to the attendee.</summary>
public sealed class RecordQrArrivalRequest
{
    public string QrId { get; set; } = string.Empty;
}

/// <summary>The result of an operator QR-door scan — the resolved
/// attendee (so the operator console can confirm WHO was recorded) plus the
/// resulting attendance state.</summary>
public sealed record QrArrivalResult(
    Guid UserId,
    string DisplayName,
    string DisplayNameArabic,
    HallAttendanceStatus Status);
