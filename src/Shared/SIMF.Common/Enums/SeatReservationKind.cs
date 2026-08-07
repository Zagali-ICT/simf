namespace SIMF.Common.Enums;

/// <summary>How a seat reservation
/// came to exist. UserBooking = a visitor picked their own seat;
/// AdminReservedRow = an admin painted a whole row as off-limits for
/// the session (no <c>ReservedForUserId</c>); RandomAssignment =
/// allocated by the system for a user who asked for a random seat;
/// OpenSeating = a visitor joined a general-admission session with no
/// specific seat (<c>RowLabel</c>/<c>SeatNumber</c> are null).</summary>
public enum SeatReservationKind
{
    UserBooking = 0,
    AdminReservedRow = 1,
    RandomAssignment = 2,
    OpenSeating = 3,
}
