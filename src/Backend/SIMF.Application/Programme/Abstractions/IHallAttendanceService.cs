using SIMF.Contracts.Sessions;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>P5.1 — D-241 (FDS-003 §5.4, FR-305/506): records an attendee's
/// arrival at and departure from a session's hall. Two means (FDS-003 §5.4):
/// the attendee's own device crossing the GPS geofence
/// (<see cref="RecordGeofenceArrivalAsync"/>, <c>AttendanceMethod.Geofence</c>)
/// and an operator scanning the badge QR at the hall door (P5.1d — D-244,
/// <see cref="RecordQrArrivalAsync"/>, <c>AttendanceMethod.QrScan</c>). Both
/// merge into the one open row. Feeds session attendance (FR-506) and the
/// question-gating-on-arrival check (FR-704).</summary>
public interface IHallAttendanceService
{
    /// <summary>Claim arrival at the session's hall from a reported GPS point.
    /// Validates the point against the hall geofence (D-240) and opens (or
    /// returns the existing open) attendance row. 404 session; 400 when the hall
    /// has no geofence; 403 when the point is outside it.</summary>
    Task<HallAttendanceStatus> RecordGeofenceArrivalAsync(
        Guid userId, Guid sessionId, double lat, double lon,
        CancellationToken cancellationToken = default);

    /// <summary>Close the attendee's open attendance row for the session (idempotent
    /// — a no-op when there is no open row).</summary>
    Task<HallAttendanceStatus> RecordDepartureAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>The attendee's current attendance state for the session (the open
    /// row if present, else the most recent closed row, else not arrived).</summary>
    Task<HallAttendanceStatus> GetStatusAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>P5.1d — D-244 (FDS-003 §5.4): an operator records a hall arrival by
    /// scanning an attendee's badge QR at the door. Resolves the QR to the attendee
    /// (<see cref="SIMF.Application.AccessControl.Abstractions.IQrResolver"/>),
    /// requires an Approved, non-locked account, and opens (or returns the existing
    /// open) attendance row with <c>Method = QrScan</c> — merging with any geofence
    /// row. No geofence is required (the operator is physically at the door).
    /// 400 unknown/blank QR; 403 non-approved attendee; 404 unknown session.</summary>
    Task<QrArrivalResult> RecordQrArrivalAsync(
        Guid operatorUserId, Guid sessionId, string qrId,
        CancellationToken cancellationToken = default);
}
