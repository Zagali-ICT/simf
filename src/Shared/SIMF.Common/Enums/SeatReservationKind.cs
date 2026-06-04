namespace SIMF.Common.Enums;

/// <summary>D-175 (gap doc G11, Mockup page 7) — how a seat reservation
/// came to exist. UserBooking = a visitor picked their own seat;
/// AdminReservedRow = an admin painted a whole row as off-limits for
/// the session (no <c>ReservedForUserId</c>); RandomAssignment =
/// allocated by the system for a user who asked for a random seat.</summary>
public enum SeatReservationKind
{
    UserBooking = 0,
    AdminReservedRow = 1,
    RandomAssignment = 2,
}
