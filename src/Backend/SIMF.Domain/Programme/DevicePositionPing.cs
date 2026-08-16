namespace SIMF.Domain.Programme;

/// <summary>
/// One periodic device-position sample, and the only continuous positional record
/// in the system: movement, dwell and route all read from here. Inert until a hall
/// has a geofence configured. Raw coordinates are sensitive personal data, so
/// capture is self-only and the aggregate reads need the attendance-view
/// permission. No FK to Hall or Session: telemetry must never block editing either.
/// </summary>
public sealed class DevicePositionPing
{
    public Guid Id { get; set; }

    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid UserId { get; set; }

    /// <summary>Whose geofence contained the point, resolved server-side at capture.</summary>
    public Guid? HallId { get; set; }

    public Guid? SessionId { get; set; }

    /// <summary>When the device took the fix, distinct from <see cref="CreatedAt"/>
    /// so a batch uploaded after a connectivity gap still orders correctly.</summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>WGS-84 decimal degrees.</summary>
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Horizontal accuracy in metres, when the device supplied one. A
    /// low-confidence fix is kept, not discarded: the aggregation decides.</summary>
    public double? AccuracyMeters { get; set; }

    public DateTime CreatedAt { get; set; }
}
