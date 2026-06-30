using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>D-175 (gap doc G11, Mockup page 7) — full seat-grid view
/// for one session. The Flutter app renders rows in
/// <see cref="RowLabels"/> order, with each cell coloured per
/// <see cref="ReservedCells"/>. <see cref="MyCell"/> is the caller's
/// own active seat (null if none).</summary>
public sealed record SessionSeatMap(
    Guid SessionId,
    Guid HallId,
    int HallCapacity,
    int? SessionCapacity,
    IReadOnlyList<string> RowLabels,
    int SeatsPerRow,
    IReadOnlyList<SessionSeatCell> ReservedCells,
    SessionSeatCell? MyCell,
    int ActiveReservedCount,
    // D-432 — appended (append-only wire): the session's bilingual title so the
    // "my seat" screen can show it without a second /sessions/{id} call. The
    // service already loads the Session, so this adds no query.
    string? SessionTitle = null,
    string? SessionTitleArabic = null,
    // D-485 — appended (append-only wire): the session's EFFECTIVE seat-selection
    // mode (Session override ?? Hall default). The app branches the "Join" CTA on
    // this — AssignedSeat shows the seat picker, OpenSeating a one-tap join.
    SeatSelectionMode Mode = SeatSelectionMode.AssignedSeat);

/// <summary>D-175 — one occupied seat in the grid. D-485: <see cref="RowLabel"/>
/// and <see cref="SeatNumber"/> are null for an OpenSeating join. D-572 appends
/// <see cref="Status"/> (append-only wire) so the app's "my seat" card can switch
/// its hint — Pending → "await approval", Approved → "show your badge at entry";
/// default Pending keeps older callers safe.</summary>
public sealed record SessionSeatCell(
    Guid ReservationId,
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    BookingStatus Status = BookingStatus.Pending);

/// <summary>D-175 — visitor self-pick request. Pass row+seat from
/// the grid the app rendered against the <see cref="SessionSeatMap"/>.
/// Open for inheritance so the route-binding endpoint can carry a
/// <c>SessionId</c> field (matches the D-168 / D-174 pattern).</summary>
public class ReserveSeatRequest
{
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
}

/// <summary>D-175 — admin row-block request. The whole row is marked
/// <see cref="SeatReservationKind.AdminReservedRow"/> for this session
/// (one reservation row per seat). Subsequent visitor picks against
/// any seat in that row are rejected with
/// <c>SEAT_ALREADY_RESERVED</c>. Open for inheritance per the
/// D-168 / D-174 pattern.</summary>
public class AdminReserveRowRequest
{
    public string RowLabel { get; set; } = string.Empty;
}

/// <summary>D-175 — admin layout edit. Writes
/// <c>RowLabels.Count * SeatsPerRow</c> grid; rejected if the product
/// exceeds <c>Hall.Capacity</c>. Open for inheritance per the
/// D-168 / D-174 pattern.</summary>
public class SetHallSeatLayoutRequest
{
    public IReadOnlyList<string> RowLabels { get; set; } = Array.Empty<string>();
    public int SeatsPerRow { get; set; }
}

/// <summary>D-175 — admin layout read-back.</summary>
public sealed record HallSeatLayoutSnapshot(
    Guid HallId,
    IReadOnlyList<string> RowLabels,
    int SeatsPerRow,
    int LayoutCapacity,
    int HallCapacity);

/// <summary>D-175 — what the user sees after a successful reservation.
/// Returned by both self-pick and random-allocate. <see cref="Status"/>
/// (P2.2 — D-227) is appended: a fresh booking is <c>Pending</c> until the
/// Control Panel approves it (the field is append-only — the shipped app
/// ignores unknown JSON keys).</summary>
public sealed record MySeatReservation(
    Guid ReservationId,
    Guid SessionId,
    // D-485: null for an OpenSeating join (general admission — no specific seat).
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    DateTimeOffset CreatedAt,
    BookingStatus Status = BookingStatus.Pending);

/// <summary>P2.2 — D-227 (FDS-005 §5.2): one row in the Control Panel booking
/// approval queue. Carries the session + seat + attendee so the reviewer can
/// decide without a drill-down. <see cref="AttendeeName"/> is resolved from
/// the Identity DB in a separate round-trip (no cross-DB JOIN, D-157).</summary>
public sealed record BookingQueueRow(
    Guid ReservationId,
    Guid SessionId,
    string SessionTitle,
    string SessionTitleArabic,
    DateTimeOffset SessionStartUtc,
    // D-485: null for an OpenSeating join — the CP renders it as "general admission".
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    Guid? AttendeeUserId,
    string AttendeeName,
    DateTimeOffset CreatedAt);

/// <summary>P2.2 — D-227: admin reject request. The reason is required
/// (FDS-005 §8) and recorded on the booking + sent to the attendee. Open for
/// inheritance so the route-binding endpoint can carry the booking id.</summary>
public class RejectBookingRequest
{
    public string Reason { get; set; } = string.Empty;
}
