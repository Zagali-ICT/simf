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
}
