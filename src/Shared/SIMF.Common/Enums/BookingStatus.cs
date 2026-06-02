namespace SIMF.Common.Enums;

/// <summary>P2.2 — D-227 (SIMF-FDS-005 §4): the approval state of a seat
/// booking. Every visitor booking (self-pick or random allocation) is created
/// <see cref="Pending"/> and the Control Panel <see cref="Approved"/>s or
/// <see cref="Rejected"/>s it (FR-503). An admin row-block is created
/// <see cref="Approved"/> directly — it is an admin action, not a visitor
/// booking subject to the queue.
/// <para>The <b>held seat</b> is still keyed on <c>ReleasedAt IS NULL</c>
/// (Pending + Approved both hold the seat, preserving the D-175 uniqueness
/// indexes). <see cref="Rejected"/> and <see cref="Cancelled"/> set
/// <c>ReleasedAt</c>, freeing the seat for re-booking.</para></summary>
public enum BookingStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}
