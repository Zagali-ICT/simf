using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>D-175 (gap doc G11, Mockup page 7) — one reserved seat
/// in one <see cref="Session"/>. Owners:
/// <list type="bullet">
/// <item>A visitor (<see cref="Kind"/> = UserBooking or RandomAssignment) —
/// <see cref="ReservedForUserId"/> is the visitor's user id (logical FK
/// to the Identity DB).</item>
/// <item>An entire row blocked by an admin (<see cref="Kind"/> =
/// AdminReservedRow) — <see cref="ReservedForUserId"/> is null and one
/// row is materialized as N rows (one per seat).</item>
/// </list>
/// Per-session uniqueness: a seat (row+number) can only be active for
/// one reservation at a time; a user can only hold one active seat
/// per session. Released reservations stay in the table for audit;
/// <see cref="ReleasedAt"/> is set and the filtered unique indexes
/// allow the seat to be re-taken.</summary>
public sealed class SeatReservation
{
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="Session"/>. Cascade delete.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Row label from <see cref="HallSeatLayout.RowLabels"/>
    /// (1–8 chars, e.g. "A", "VIP").</summary>
    public string RowLabel { get; set; } = string.Empty;

    /// <summary>1-based seat number within the row
    /// (1 .. <see cref="HallSeatLayout.SeatsPerRow"/>).</summary>
    public int SeatNumber { get; set; }

    public SeatReservationKind Kind { get; set; }

    /// <summary>Logical FK to SimfUser.Id on the Identity DB. Null
    /// when <see cref="Kind"/> = AdminReservedRow.</summary>
    public Guid? ReservedForUserId { get; set; }

    /// <summary>Who created the reservation — the visitor for self
    /// bookings, the admin who blocked the row for
    /// <see cref="SeatReservationKind.AdminReservedRow"/>.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the reservation is released (visitor cancels
    /// or admin clears). Released rows are excluded from the unique
    /// indexes so the seat is bookable again.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}
