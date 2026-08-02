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

    /// <summary>B1 (owner "change seat") — move the caller's already-held seat to
    /// another seat in the SAME session. Atomic: the destination is acquired and the
    /// source released inside one transaction, so a lost race (the target taken
    /// between the read and the write) rolls the whole move back and leaves the
    /// caller on their original seat — never seatless. Re-runs the initial
    /// reservation's rules: seat bounds, tier eligibility (a non-VIP visitor cannot
    /// move into a VIP seat; nobody self-moves into a VVIP seat) and, per the owner's
    /// cancel boundary, only BEFORE the session starts
    /// (<c>BOOKING_SESSION_STARTED</c> after that).</summary>
    Task<MySeatReservation> MoveAsync(
        Guid sessionId, Guid actorUserId,
        MoveSeatRequest request,
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

    /// <summary>B15 — remove the hall's seat layout entirely, converting the hall
    /// back to general admission (no seat picker; <c>EffectiveMode</c> falls to
    /// <c>OpenSeating</c> for every session in it). A hall with no layout is a 404
    /// <c>SEAT_LAYOUT_MISSING</c>. Refuses with 409
    /// <c>SEAT_LAYOUT_HAS_RESERVATIONS</c> — naming how many block it — when any
    /// active seat-specific reservation exists across the hall's sessions, the same
    /// rule <c>SetLayoutAsync</c> applies when a change would strand a seat: the
    /// operator releases those seats first. Returns the now-empty snapshot.</summary>
    Task<HallSeatLayoutSnapshot> DeleteLayoutAsync(
        Guid actorUserId, Guid hallId,
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

    /// <summary>The Control Panel seat plan's active reservations. Returns
    /// <see cref="SeatPlanCell"/> — the ADMIN shape — so each row names its holder
    /// and carries the real booking status + check-in flag (DEF-SEA-001 / A11).
    /// The app-facing seat map keeps <see cref="SessionSeatCell"/>, which has no
    /// attendee identity on it.</summary>
    Task<GridPage<SeatPlanCell>> ListSessionReservationsAsync(
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
        DateTime now, CancellationToken cancellationToken = default);

    // -- Staff seating desk (D-771 — owner 2026-07-26) --

    /// <summary>D-771 — "who sits here?": resolve one seat in a session to its
    /// occupant (reference id, bilingual name, whether a photo can be streamed) or,
    /// for a VVIP protocol seat, to the administrator's manual guest hint.
    /// <c>Found = false</c> means the seat is free — a valid answer, not an
    /// error.</summary>
    Task<StaffSeatOccupant> ResolveSeatOccupantAsync(
        Guid sessionId, string rowLabel, int seatNumber,
        CancellationToken cancellationToken = default);

    /// <summary>D-771 — "where do I sit?": resolve a scanned badge QR id to the
    /// holder's seat in this session. <c>Found = false</c> with the holder's identity
    /// filled in means the badge is valid but holds no seat here; an unknown badge
    /// throws <c>ATTENDEE_QR_UNKNOWN</c>.</summary>
    Task<StaffSeatOccupant> ResolveBadgeSeatAsync(
        Guid sessionId, string qrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// D-819 — records that a walk-in admitted to a session hall is occupying a
    /// place, so the seating desk and the occupancy counts can see them.
    ///
    /// <para>ADVISORY, and deliberately unlike every other reservation path
    /// here. It never throws and it never blocks: the person has already been
    /// admitted through the door, so failing to record a seat must not undo
    /// that. Returns false when they already hold a reservation for the session
    /// (the filtered unique index on (SessionId, ReservedForUserId) is the
    /// guard) or when the insert lost a race.</para>
    ///
    /// <para>Creates an <c>OpenSeating</c> hold with a null row and seat rather
    /// than assigning a specific place. Picking a seat runs inside a SERIALIZABLE
    /// transaction that counts capacity first, and running that per door scan
    /// would deadlock-storm a rush; a null row and seat also never touch the
    /// per-seat filtered unique index. <c>Expires</c> is left null so the no-show
    /// sweep cannot release someone who is physically present.</para>
    /// </summary>
    Task<bool> EnsureWalkInHoldAsync(
        Guid sessionId, Guid attendeeUserId, CancellationToken cancellationToken = default);
}
