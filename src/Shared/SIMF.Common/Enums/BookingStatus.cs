namespace SIMF.Common.Enums;

/// <summary>The lifecycle state of a seat
/// booking. Introduced for a Control Panel approval queue; the owner removed that
/// queue on 2026-07-18 ("reservation-only" — a booking is confirmed the moment it
/// is made), so the as-built state machine is narrower than the enum:
/// <list type="bullet">
/// <item><see cref="Approved"/> — written by EVERY create path (visitor self-pick,
/// random allocation, open-seating join, admin row/seat block). This is the only
/// state a live, held reservation is ever in.</item>
/// <item><see cref="Cancelled"/> — written together with <c>ReleasedAt</c> when a
/// visitor cancels, an admin releases the seat, the pre-start no-show sweep frees
/// it, or the session is cancelled.</item>
/// <item><see cref="Pending"/> — NO production writer. It is the
/// zero value, so it is what an unset field and a wire payload that omits the key
/// read as, and it is the C# parameter default on the contract records; nothing
/// persists it.</item>
/// <item><see cref="Rejected"/> — NO production writer. There is no reject
/// action left to write it. <c>SeatReservation.RejectionReason</c> and
/// <c>ErrorCodes.BOOKING_REJECTION_REASON_REQUIRED</c> are its equally vestigial
/// companions.</item>
/// </list>
/// The two unwritten values are KEPT, not deleted: the enum is int-backed and
/// frozen against renumbering, <c>Rejected = 2</c> is decoded by the
/// shipped mobile app, and read-side mappings (e.g. the requests screen's status
/// mapping) still switch on it. Treat them as reserved, and do not build new
/// behaviour on them without an owner decision to restore an approval step.
/// <para>The <b>held seat</b> is keyed on <c>ReleasedAt IS NULL</c>, preserving the
/// seat uniqueness indexes; <see cref="Cancelled"/> sets <c>ReleasedAt</c>, freeing
/// the seat for re-booking.</para></summary>
public enum BookingStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}
