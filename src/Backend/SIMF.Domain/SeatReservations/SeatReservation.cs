using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>One reserved seat in one <see cref="Session"/>, held by a visitor or
/// blocked off by an admin. Blocking a whole row writes one row here per seat,
/// never a range.</summary>
public sealed class SeatReservation
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    // Null together on general admission; set together on a seat-specific kind.
    public string? RowLabel { get; set; }

    public int? SeatNumber { get; set; }

    public SeatReservationKind Kind { get; set; }

    /// <summary>The holder, keyed by profile because an attendee need not hold an
    /// account. Null on an admin block, which is what tells blocked from taken.</summary>
    public Guid? ReservedForProfileId { get; set; }
    public Profiles.UserProfile? ReservedForProfile { get; set; }

    /// <summary>Who made the reservation, not always who holds it: the visitor on a
    /// self-booking, the admin who blocked the row otherwise.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Stamped when the seat is given up; the row is kept rather than
    /// deleted, and the seat becomes bookable again.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>Narrower than the enum, there being no approval step: a live row is
    /// Approved, a released row Cancelled, and nothing writes the other two.</summary>
    public BookingStatus Status { get; set; }

    /// <summary><b>Not a review decision, despite the name.</b> Read it as "who
    /// released this"; only a Control Panel seat-plan release writes it.</summary>
    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>The no-show deadline: past it the sweep releases the seat unless the
    /// holder has checked in. Null exempts the row, the holder being present already
    /// — admin blocks and walk-in holds.</summary>
    public DateTime? Expires { get; set; }

    // No registration stands behind a blocked VVIP seat, so this typed hint IS the
    // occupant record, e.g. "Reserved for the Minister". AdminReservedRow only.
    public string? GuestHint { get; set; }

    public string? GuestHintArabic { get; set; }
}
