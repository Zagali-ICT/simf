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
    /// (1–8 chars, e.g. "A", "VIP"). D-485: <b>null</b> for an
    /// <see cref="SeatReservationKind.OpenSeating"/> join (general admission —
    /// no specific seat); always set for the seat-specific kinds.</summary>
    public string? RowLabel { get; set; }

    /// <summary>1-based seat number within the row
    /// (1 .. <see cref="HallSeatLayout.SeatsPerRow"/>). D-485: <b>null</b> for an
    /// <see cref="SeatReservationKind.OpenSeating"/> join; paired with
    /// <see cref="RowLabel"/> (both null together, or both set together).</summary>
    public int? SeatNumber { get; set; }

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

    /// <summary>P2.2 — D-227 (FDS-005 §4): the booking lifecycle state.
    /// <para>A9 (2026-07-27) — as built there is NO approval step: the owner made
    /// bookings reservation-only on 2026-07-18, so every create path (visitor
    /// self-pick, random allocation, open-seating join, admin row/seat block)
    /// writes <see cref="BookingStatus.Approved"/>, and the only other value ever
    /// written is <see cref="BookingStatus.Cancelled"/>, always together with
    /// <see cref="ReleasedAt"/>. A live row is therefore always Approved; a
    /// released row is Cancelled. <see cref="BookingStatus.Pending"/> and
    /// <see cref="BookingStatus.Rejected"/> have no writer — see
    /// <see cref="BookingStatus"/> for why they are kept.</para></summary>
    public BookingStatus Status { get; set; }

    /// <summary>P2.2 — originally "the admin who approved or rejected the booking".
    /// <para>A9 (2026-07-27) — with the approval queue gone this is written by
    /// exactly one path: an admin RELEASE from the Control Panel seat plan stamps
    /// the acting admin here. Read it as "who released this reservation", not as a
    /// review decision. Null on every live row. Logical FK to SimfUser on the
    /// Identity DB (no constraint — D-157).</para></summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>P2.2 — the timestamp paired with <see cref="ReviewedByUserId"/>.
    /// A9: written only by an admin RELEASE (alongside <see cref="ReleasedAt"/>);
    /// null on every live row.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>P2.2 — the reason recorded when a booking is rejected (FDS-005 §8).
    /// <para>A9 (2026-07-27) — VESTIGIAL: no code path writes it, because the reject
    /// action it belonged to was removed with the approval queue. The column is kept
    /// (nullable, ≤512 chars, always NULL) rather than dropped — dropping it is a
    /// destructive schema change against a frozen baseline, and it is the landing
    /// spot if an approval step is ever restored. Do not read it expecting a
    /// value.</para></summary>
    public string? RejectionReason { get; set; }

    /// <summary>M-6 — when a Pending, still-held visitor booking auto-expires and
    /// frees its seat. Set at creation to CreatedAt + the hold window on the
    /// visitor-booking kinds (UserBooking / RandomAssignment / OpenSeating); NULL
    /// for an AdminReservedRow block (admin holds never expire). Existing rows and
    /// already-decided bookings carry NULL and are ignored by the expiry worker.</summary>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>D-771 (owner 2026-07-26) — the manual guest HINT an administrator
    /// types when blocking a VVIP seat from the Control Panel. A VVIP seat has no
    /// registration, so there is no <see cref="ReservedForUserId"/> to resolve a
    /// name from: the hint IS the occupant record (e.g. "Reserved for the
    /// Minister"). Only meaningful on a
    /// <see cref="SeatReservationKind.AdminReservedRow"/> block; null everywhere
    /// else. ≤256 chars.</summary>
    public string? GuestHint { get; set; }

    /// <summary>D-771 — the Arabic twin of <see cref="GuestHint"/> (the surrounding
    /// entities are all bilingual), e.g. "هذا المقعد محجوز لمعالي الوزير". ≤256
    /// chars; null when the admin typed only one language.</summary>
    public string? GuestHintArabic { get; set; }
}
