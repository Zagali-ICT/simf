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

    Task AdminReleaseAsync(
        Guid actorUserId, Guid sessionId, Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<GridPage<SessionSeatCell>> ListSessionReservationsAsync(
        Guid sessionId, GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Booking approval queue (P2.2 / D-227 — FDS-005 §5.2) --

    /// <summary>The Control Panel approval queue: Pending, still-held visitor
    /// bookings across all sessions, newest first.</summary>
    Task<GridPage<BookingQueueRow>> ListPendingBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Approve a Pending booking — the held seat is confirmed and a
    /// booking-confirmed event is raised.</summary>
    Task ApproveBookingAsync(
        Guid actorUserId, Guid reservationId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a Pending booking with a reason — the held seat is
    /// released and the attendee is notified.</summary>
    Task RejectBookingAsync(
        Guid actorUserId, Guid reservationId, RejectBookingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk-approve several Pending bookings; returns the number
    /// actually approved (already-decided / missing ids are skipped).</summary>
    Task<int> BulkApproveBookingsAsync(
        Guid actorUserId, IReadOnlyList<Guid> reservationIds,
        CancellationToken cancellationToken = default);
}
