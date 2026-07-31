namespace SIMF.Contracts.Programme;

/// <summary>FR-1103 (Q6) — the body of
/// <c>POST /api/v1/app/movement/pings</c>. The attendee's own device reports a
/// batch of position samples taken since the last upload, so a connectivity gap
/// does not lose the track. Self-only: the caller can post nothing but their own
/// position (the actor comes from the <c>sub</c> claim, never the body).</summary>
public sealed class RecordDevicePositionsRequest
{
    /// <summary>The samples, in any order — the server orders by
    /// <see cref="DevicePositionSample.CapturedAt"/>. Capped server-side.</summary>
    public List<DevicePositionSample> Samples { get; set; } = [];
}

/// <summary>FR-1103 — one position sample taken by the device.</summary>
/// <param name="CapturedAt">When the DEVICE took the fix (not when it uploaded).</param>
/// <param name="Lat">WGS-84 latitude in decimal degrees, -90..90.</param>
/// <param name="Lon">WGS-84 longitude in decimal degrees, -180..180.</param>
/// <param name="AccuracyMeters">The device's reported horizontal accuracy, when it
/// supplied one.</param>
public sealed record DevicePositionSample(
    DateTimeOffset CapturedAt,
    double Lat,
    double Lon,
    double? AccuracyMeters = null);

/// <summary>FR-1103 — what the server did with an upload.</summary>
/// <param name="Accepted">How many samples were stored.</param>
/// <param name="MatchedToHall">How many of them fell inside a configured hall
/// geofence. Zero while no hall has a boundary — the state the system ships in —
/// which is how the caller can tell the feature is still inert.</param>
public sealed record RecordDevicePositionsResponse(int Accepted, int MatchedToHall);

/// <summary>FR-1103 — dwell time inside one hall over the queried window,
/// aggregated from the raw pings.</summary>
/// <param name="HallId">The hall.</param>
/// <param name="HallName">Its English name, or null when the hall no longer
/// resolves (the pings carry no FK by design).</param>
/// <param name="HallNameArabic">Its Arabic name, when it resolves.</param>
/// <param name="DistinctAttendees">How many different attendees were seen inside.</param>
/// <param name="TotalDwellMinutes">Summed dwell across those attendees.</param>
/// <param name="AverageDwellMinutes">Mean dwell per attendee seen.</param>
public sealed record HallDwellSummary(
    Guid HallId,
    string? HallName,
    string? HallNameArabic,
    int DistinctAttendees,
    double TotalDwellMinutes,
    double AverageDwellMinutes);

/// <summary>FR-1103 — one attendee's route through the venue: the ordered legs
/// derived from their pings.</summary>
/// <param name="UserId">The attendee.</param>
/// <param name="Legs">Ordered by <see cref="RouteLeg.Enter"/>.</param>
public sealed record AttendeeRoute(Guid UserId, IReadOnlyList<RouteLeg> Legs);

/// <summary>FR-1103 — one continuous stay inside one hall.</summary>
/// <param name="HallId">The hall, or null for a leg spent outside every
/// configured boundary (the walk between halls).</param>
/// <param name="HallName">Its English name when it resolves.</param>
/// <param name="HallNameArabic">Its Arabic name when it resolves.</param>
/// <param name="Enter">First ping of the stay.</param>
/// <param name="Leave">Last ping of the stay.</param>
/// <param name="DwellMinutes">Minutes between the two.</param>
public sealed record RouteLeg(
    Guid? HallId,
    string? HallName,
    string? HallNameArabic,
    DateTimeOffset Enter,
    DateTimeOffset Leave,
    double DwellMinutes);
