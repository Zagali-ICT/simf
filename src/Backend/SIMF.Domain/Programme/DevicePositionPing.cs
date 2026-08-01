namespace SIMF.Domain.Programme;

/// <summary>
/// FR-1103 (owner decision Q6) — one periodic device-position sample. This is the
/// capture path movement / dwell / route tracking never had: before it,
/// <see cref="HallAttendance"/> was the only positional record and it is
/// deliberately just an arrival/departure PAIR, "not a continuous track (that is
/// the deferred movement/dwell feature, FR-1103)". A repo-wide search for
/// <c>dwell</c> or <c>MovementTrack</c> found only that comment.
///
/// <para><b>Inert until a hall has a boundary.</b> The capture endpoint resolves
/// <see cref="HallId"/> by testing the reported point against each hall's
/// configured geofence (<c>Hall.GeofenceCenterLat</c> / <c>Lon</c> /
/// <c>RadiusMeters</c>). While no hall has one — the state the system ships in,
/// pending the venue-boundary decision — every ping simply lands with a null
/// <see cref="HallId"/> and the dwell aggregation reports nothing. Nothing else in
/// the system reads or depends on these rows, so the feature costs nothing until
/// the boundaries are configured from the CP.</para>
///
/// <para><b>GPS is sensitive personal data</b> (FDS-003 §10). Unlike
/// <see cref="HallAttendance"/> — which deliberately keeps only the derived
/// enter/leave times — this row DOES hold raw coordinates, because a route
/// projection cannot be derived without them. Two consequences are load-bearing:
/// the capture endpoint is self-only (a caller can post only their own position),
/// and the aggregate reads are gated on <c>Attendance.View</c>.</para>
///
/// <para><b>No FK to Hall or Session</b> even though both live in this same
/// database: these are raw telemetry rows written at device cadence, and a
/// telemetry row must never be the reason a hall cannot be edited or a session
/// removed. The aggregation resolves names by lookup and simply skips an id that
/// no longer resolves.</para>
/// </summary>
public sealed class DevicePositionPing
{
    public Guid Id { get; set; }

    /// <summary>Bare <c>Guid</c> of the attendee whose device reported the
    /// position — cross-context (Identity DB), no FK (D-157).</summary>
    public Guid UserId { get; set; }

    /// <summary>The hall whose configured geofence contained the reported point,
    /// resolved server-side at capture. Null when the point was inside no
    /// configured boundary (including the case where no hall has one yet).</summary>
    public Guid? HallId { get; set; }

    /// <summary>The session running in <see cref="HallId"/> at
    /// <see cref="CapturedAt"/>, when one was. Null when the hall was between
    /// sessions or <see cref="HallId"/> is null.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>When the DEVICE took the fix. Kept distinct from
    /// <see cref="CreatedAt"/> so a batch uploaded after a connectivity gap still
    /// orders correctly in the route projection.</summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>WGS-84 latitude in decimal degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>WGS-84 longitude in decimal degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>The device's own reported horizontal accuracy in metres, when it
    /// supplied one. Null otherwise; a low-confidence fix is kept rather than
    /// discarded so the aggregation can decide.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>When the server accepted the ping.</summary>
    public DateTime CreatedAt { get; set; }
}
