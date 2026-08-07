namespace SIMF.Domain.Programme;

/// <summary>
/// One periodic device-position sample, and the only continuous positional
/// record in the system. <see cref="HallAttendance"/> is deliberately just an
/// arrival/departure pair, so movement, dwell and route all read from here.
///
/// <para><b>Inert until a hall has a boundary.</b> The capture endpoint resolves
/// <see cref="HallId"/> by testing the reported point against each hall's
/// configured geofence. While no hall has one — the state the system ships in,
/// pending the venue-boundary decision — every ping lands with a null
/// <see cref="HallId"/> and the dwell aggregation reports nothing. Nothing else
/// reads these rows, so the feature costs nothing until the boundaries are
/// configured.</para>
///
/// <para><b>GPS is sensitive personal data.</b> Unlike <see cref="HallAttendance"/>,
/// which keeps only the derived enter and leave times, this row holds raw
/// coordinates, because a route cannot be reconstructed without them. Two
/// consequences are load-bearing: the capture endpoint is self-only, so a caller
/// can post nothing but their own position, and the aggregate reads are gated on
/// the attendance-view permission.</para>
///
/// <para>No foreign key to Hall or Session, even though both live in this
/// database. These are raw telemetry rows written at device cadence, and a
/// telemetry row must never be the reason a hall cannot be edited or a session
/// removed; the aggregation resolves names by lookup and skips an id that no
/// longer resolves.</para>
/// </summary>
public sealed class DevicePositionPing
{
    public Guid Id { get; set; }

    /// <summary>The attendee whose device reported the position. A bare Guid
    /// rather than a navigation: the user lives in the Identity database, so
    /// there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>The hall whose geofence contained the reported point, resolved
    /// server-side at capture. Null when the point was inside no configured
    /// boundary, including the case where no hall has one yet.</summary>
    public Guid? HallId { get; set; }

    /// <summary>The session running in <see cref="HallId"/> at
    /// <see cref="CapturedAt"/>, when one was. Null when the hall was between
    /// sessions or <see cref="HallId"/> is null.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>When the device took the fix, kept distinct from
    /// <see cref="CreatedAt"/> so a batch uploaded after a connectivity gap
    /// still orders correctly in the route projection.</summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>WGS-84 decimal degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>WGS-84 decimal degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>The device's own reported horizontal accuracy in metres, when it
    /// supplied one. A low-confidence fix is kept rather than discarded, so the
    /// aggregation decides what to do with it.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>When the server accepted the ping.</summary>
    public DateTime CreatedAt { get; set; }
}
