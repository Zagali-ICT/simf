using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>
/// FR-1103 (owner decision Q6) — movement / dwell / route tracking. Before this
/// there was no capture path at all: <c>HallAttendance</c> is deliberately an
/// arrival/departure PAIR and its own XML doc says so ("not a continuous track —
/// that is the deferred movement/dwell feature, FR-1103").
///
/// <para>Three operations, one per half of the requirement: capture the raw pings,
/// aggregate dwell per hall, and project one attendee's route. The capture side is
/// self-service (the attendee's own device); both read sides are admin reporting
/// and are gated on <c>Attendance.View</c>.</para>
///
/// <para><b>Inert by design.</b> A ping is bound to a hall by testing it against
/// that hall's configured geofence. Until a hall is given one from the CP, every
/// ping lands unmatched and both reads return nothing — the feature is built and
/// dormant, which is exactly what Q6 asked for.</para>
/// </summary>
public interface IMovementTrackingService
{
    /// <summary>Stores a batch of the caller's own position samples, resolving each
    /// one to the hall whose geofence contains it (and to that hall's running
    /// session, when any). Returns how many were stored and how many matched a
    /// hall.</summary>
    Task<RecordDevicePositionsResponse> RecordPositionsAsync(
        Guid userId,
        RecordDevicePositionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Dwell per hall over <paramref name="from"/>..<paramref name="to"/>,
    /// aggregated from the raw pings: for each hall, how many distinct attendees
    /// were seen inside and how long they stayed. Halls with no pings are absent.
    /// Ordered by total dwell, descending.</summary>
    Task<IReadOnlyList<HallDwellSummary>> DwellByHallAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>One attendee's ordered route over
    /// <paramref name="from"/>..<paramref name="to"/> — their pings collapsed into
    /// consecutive stays, including the unmatched legs between halls. Empty when
    /// the attendee reported nothing in the window.</summary>
    Task<AttendeeRoute> RouteForAttendeeAsync(
        Guid userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
