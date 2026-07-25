using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.SeatReservations.Abstractions;

/// <summary>D-175 (gap doc G11, Mockup page 7) — per-session seat
/// reservations. Public surface for visitors plus admin surface for
/// hall layout + row-blocking + release.</summary>
public interface ISeatReservationService
{
    Task<SessionSeatMap> GetSessionSeatMapAsync(
        Guid sessionId, Guid? actorUserId,
        CancellationToken cancellationToken = default);

    Task<MySeatReservation> ReserveAsync(
        Guid sessionId, Guid actorUserId,
        ReserveSeatRequest request,
        CancellationToken cancellationToken = default);

    Task<MySeatReservation> ReserveRandomAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>D-485 — join an OPEN-SEATING session (general admission): no seat
    /// is chosen, the reservation carries a null row/seat and is created Pending,
    /// just like a seat booking. Rejected with <c>SEAT_SELECTION_REQUIRED</c> if
    /// the session's effective mode is AssignedSeat.</summary>
    Task<MySeatReservation> JoinOpenSeatingAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task ReleaseMineAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default);

    // -- Admin surface --
    Task<HallSeatLayoutSnapshot> GetLayoutAsync(
        Guid hallId, CancellationToken cancellationToken = default);

    Task<HallSeatLayoutSnapshot> SetLayoutAsync(
        Guid actorUserId, Guid hallId,
        SetHallSeatLayoutRequest request,
        CancellationToken cancellationToken = default);

    Task AdminReserveRowAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveRowRequest request,
        CancellationToken cancellationToken = default);

    Task AdminReserveSeatAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveSeatRequest request,
        CancellationToken cancellationToken = default);

    Task AdminReleaseAsync(
        Guid actorUserId, Guid sessionId, Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<GridPage<SessionSeatCell>> ListSessionReservationsAsync(
        Guid sessionId, GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Booking monitor + no-show release (#6/#17 — owner 2026-07-20) --

    /// <summary>The read-only Control Panel monitor: ACTIVE (confirmed, still-held)
    /// visitor reservations across all sessions, newest first. There is no approval
    /// step — bookings auto-confirm — so this is a monitor, not a queue.</summary>
    Task<GridPage<ActiveBookingRow>> ListActiveBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>#6/#17 — the pre-start no-show sweep: release every active
    /// (Approved, still-held) visitor reservation whose no-show deadline
    /// (<c>Start − 3min</c>) has passed and whose holder never checked in, freeing
    /// the seat for others. Returns the number released. Called once per minute by
    /// <c>ReservationNoShowReleaseWorker</c>.</summary>
    Task<int> ReleaseNoShowsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);
}
