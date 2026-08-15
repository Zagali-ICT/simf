using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>
/// One reserved seat in one <see cref="Session"/>, held either by a visitor or by
/// an admin blocking the seat off.
///
/// <para>A visitor hold carries the holder in <see cref="ReservedForProfileId"/>. An
/// admin block (<see cref="SeatReservationKind.AdminReservedRow"/>) has no holder,
/// and blocking a whole row writes one row here per seat rather than a single
/// range.</para>
///
/// <para>Two uniqueness rules hold while a reservation is live: a seat (row and
/// number) belongs to one reservation per session, and a user holds one seat per
/// session. Releasing does not delete the row, so the history stays auditable.
/// <see cref="ReleasedAt"/> is stamped instead, and because both unique indexes
/// are filtered on it being null, the seat becomes bookable again.</para>
/// </summary>
public sealed class SeatReservation
{
    public Guid Id { get; set; }

    /// <summary>A real foreign key with delete restricted, so deleting a session
    /// cannot silently wipe its seat reservations — they are released or cancelled
    /// explicitly first.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    // The seat itself. Both null together on an OpenSeating join, which is general
    // admission and grants no particular place; both set together on every
    // seat-specific kind. RowLabel comes from HallSeatLayout.RowLabels (1 to 8
    // chars, e.g. "A" or "VIP"); SeatNumber is 1-based within that row, up to the
    // layout's seats per row.
    public string? RowLabel { get; set; }

    public int? SeatNumber { get; set; }

    public SeatReservationKind Kind { get; set; }

    /// <summary>The holder, keyed by their <see cref="Profiles.UserProfile"/> —
    /// the row every attendee has, whether or not they hold an account. It was the
    /// Identity account id until the profile became the attendee record, which
    /// meant a walk-in admitted at the door had nowhere to hold the seat they were
    /// standing in. Null on an admin block, which is what tells a blocked seat from
    /// a taken one, so the foreign key is optional.</summary>
    public Guid? ReservedForProfileId { get; set; }
    public Profiles.UserProfile? ReservedForProfile { get; set; }

    /// <summary>Who made the reservation, which is not always who holds it: the
    /// visitor on a self-booking, the admin who blocked the row otherwise.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Stamped when the seat is given up, whether the visitor cancels, an
    /// admin clears it, or the no-show sweep frees it. A released row falls out of
    /// the filtered unique indexes, so the seat is bookable again.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>Narrower in practice than the enum suggests, because there is no
    /// approval step: every create path writes <see cref="BookingStatus.Approved"/>,
    /// and the only other value ever written is <see cref="BookingStatus.Cancelled"/>,
    /// always together with <see cref="ReleasedAt"/>. So a live row is Approved and a
    /// released row is Cancelled. See <see cref="BookingStatus"/> for why the two
    /// values nothing writes are kept.</summary>
    public BookingStatus Status { get; set; }

    /// <summary><b>Not a review decision, despite the name.</b> One path writes it:
    /// an admin releasing a seat from the Control Panel seat plan stamps themselves
    /// here. Read it as "who released this". Null on every live row, and a bare Guid
    /// because the user is in the Identity database.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>Written with <see cref="ReviewedByUserId"/> by that same admin
    /// release, alongside <see cref="ReleasedAt"/>. Null on every live row.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>The no-show deadline, stamped at creation as the session's start
    /// minus SeatReservationService.NoShowReleaseGrace. Once it passes, the sweep
    /// releases the seat unless the holder has checked in. Null means the sweep can
    /// never touch the row: admin blocks, and the walk-in hold created at the door
    /// for someone already in the hall. A seat booked at or after the deadline is
    /// exempt for the same reason, the holder is there.</summary>
    public DateTime? Expires { get; set; }

    // The occupant of a VVIP seat an admin has blocked. Such a seat has no
    // registration behind it, so there is no ReservedForProfileId to resolve a name
    // from and this typed hint IS the occupant record, e.g. "Reserved for the
    // Minister". Meaningful only on an AdminReservedRow block, null everywhere else.
    // At most 256 chars each, and the Arabic twin is null when the admin typed one
    // language only.
    public string? GuestHint { get; set; }

    public string? GuestHintArabic { get; set; }
}
