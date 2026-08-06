using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>Full seat-grid view
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
    // Appended (append-only wire): the session's bilingual title so the
    // "my seat" screen can show it without a second /sessions/{id} call. The
    // service already loads the Session, so this adds no query.
    string? SessionTitle = null,
    string? SessionTitleArabic = null,
    // Appended (append-only wire): the session's EFFECTIVE seat-selection
    // mode (Session override ?? Hall default). The app branches the "Join" CTA on
    // this — AssignedSeat shows the seat picker, OpenSeating a one-tap join.
    SeatSelectionMode Mode = SeatSelectionMode.AssignedSeat,
    // Appended (append-only wire): optional per-row seat counts, PARALLEL to
    // <see cref="RowLabels"/>, when the hall layout is ragged. Null (key omitted) for a
    // uniform layout — old and new apps render every row at <see cref="SeatsPerRow"/>.
    // When present, row i has SeatCounts[i] seats and SeatsPerRow carries max(SeatCounts).
    IReadOnlyList<int>? SeatCounts = null,
    // Appended (append-only wire): the per-row seat TIERS, PARALLEL to
    // <see cref="RowLabels"/>. Null (key omitted) only for a degraded/legacy read;
    // the service always emits one entry per row,
    // so a new app can colour VVIP / VIP / Normal rows and grey out the seats the
    // caller may not book. An older app ignores the key and renders as before.
    IReadOnlyList<SeatTier>? SeatTiers = null,
    // Appended: whether the CALLER is a VIP-tier visitor (their
    // ProfileType.AllowsVipMeetingSlots — the same flag the app reads as isVip).
    // The app uses it to grey out VIP rows for a non-VIP visitor; the server
    // re-checks it on every reserve, so this is a UX hint, never the gate.
    bool CallerIsVip = false);

/// <summary>One occupied seat in the grid. <see cref="RowLabel"/>
/// and <see cref="SeatNumber"/> are null for an OpenSeating join.
/// <see cref="Status"/> is appended (append-only wire) so the app's "my seat" card can switch
/// its hint; default Pending keeps older callers safe.
/// <para>As built, no production path ever writes
/// <see cref="BookingStatus.Pending"/>: every create path stamps
/// <see cref="BookingStatus.Approved"/> (bookings auto-confirm — see
/// <see cref="BookingStatus"/>), so a live cell always reads Approved and the
/// Pending default is only the safety value for a payload that omits the key.</para>
/// <para><see cref="CheckedIn"/> is appended (append-only wire):
/// the holder has an OPEN <c>HallAttendance</c> row for this session — they scanned
/// in at the hall gate ("تم التأكيد" / confirmed). The four app/CP seat states are:
/// available (no cell) · unavailable (<c>Kind == AdminReservedRow</c>) · reserved
/// (a holder, <c>CheckedIn == false</c>) · confirmed (a holder, <c>CheckedIn ==
/// true</c>). Default false keeps older callers safe.</para></summary>
public sealed record SessionSeatCell(
    Guid ReservationId,
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    BookingStatus Status = BookingStatus.Pending,
    bool CheckedIn = false,
    // Appended (append-only wire): the admin-typed VVIP guest hint. A VVIP
    // seat has no registration, so the hint is the occupant record the app and the
    // staff seating screen display ("Reserved for the Minister"). Null on every
    // ordinary reservation.
    string? GuestHint = null,
    string? GuestHintArabic = null);

/// <summary>One active reservation on the
/// Control Panel seat plan (<c>POST /admin/sessions/{id}/seats/list</c>). It is a
/// SEPARATE shape from <see cref="SessionSeatCell"/> on purpose: the seat plan is
/// an admin surface that must name WHO holds a seat before an administrator
/// releases it, and keeping that identity off the shared cell makes it impossible
/// for the app-facing seat map (<c>GET /app/sessions/{id}/seats</c>, which reuses
/// <see cref="SessionSeatCell"/>) to start carrying attendee names by accident.
/// <para><see cref="Status"/> and <see cref="CheckedIn"/> are REAL values
/// here, never record defaults: the seat plan's own projection reads the stored
/// status and resolves the open <c>HallAttendance</c> row, so the four seat
/// states (available · unavailable · reserved · confirmed) are all decidable from
/// this row.</para>
/// <para>Cross-DB safe: <see cref="HolderUserId"/> is a bare logical
/// user id and the bilingual holder name comes from the App-side
/// <c>UserProfile</c> in a second query — no cross-database join and nothing
/// duplicated.</para></summary>
public sealed record SeatPlanCell(
    Guid ReservationId,
    // Null for an OpenSeating join (general admission — no specific seat).
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    BookingStatus Status,
    // True when the holder has an OPEN HallAttendance row for this session — the
    // "confirmed" seat state, as opposed to a merely held "reserved" one.
    bool CheckedIn,
    // The holder's user id (logical FK to the Identity DB). Null for an admin
    // row-block / VVIP protocol seat, which has no registration.
    Guid? HolderUserId,
    // The holder's bilingual display name, resolved from the App-side UserProfile.
    // Empty for an admin block / VVIP seat — read the guest hint instead.
    string HolderName,
    string HolderNameArabic,
    // The admin-typed VVIP guest note: the occupant record for a seat that
    // has no registration. Null on an ordinary attendee reservation.
    string? GuestHint,
    string? GuestHintArabic);

/// <summary>Visitor self-pick request. Pass row+seat from
/// the grid the app rendered against the <see cref="SessionSeatMap"/>.
/// Open for inheritance so the route-binding endpoint can carry a
/// <c>SessionId</c> field.</summary>
public class ReserveSeatRequest
{
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
}

/// <summary>Move an EXISTING held seat to another one
/// in the same session, atomically: the destination is acquired and the source
/// released in a single unit of work, so a lost race leaves the caller on their
/// original seat. Same shape as <see cref="ReserveSeatRequest"/> — the row+seat
/// the app read off the <see cref="SessionSeatMap"/> — and open for inheritance so
/// the route-binding endpoint can carry a <c>SessionId</c>
/// field.</summary>
public class MoveSeatRequest
{
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
}

/// <summary>Admin row-block request. The whole row is marked
/// <see cref="SeatReservationKind.AdminReservedRow"/> for this session
/// (one reservation row per seat). Subsequent visitor picks against
/// any seat in that row are rejected with
/// <c>SEAT_ALREADY_RESERVED</c>. Open for inheritance so the
/// route-binding endpoint can carry a <c>SessionId</c> field.</summary>
public class AdminReserveRowRequest
{
    public string RowLabel { get; set; } = string.Empty;
}

/// <summary>2026-07-18 — admin single-seat block (VIP hold). Marks ONE specific
/// seat <see cref="SeatReservationKind.AdminReservedRow"/> for this session so it
/// is held (unavailable to visitors) for a VIP; a visitor pick on that seat is
/// rejected with <c>SEAT_ALREADY_RESERVED</c>. Released like any admin block.</summary>
public class AdminReserveSeatRequest
{
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }

    /// <summary>The free-text guest hint for a VVIP seat (there is no
    /// registration, so the admin types the guest's name/note here). Optional,
    /// ≤256 chars; bilingual like the surrounding entities.</summary>
    public string? GuestHint { get; set; }

    /// <summary>The Arabic twin of <see cref="GuestHint"/>, e.g.
    /// "هذا المقعد محجوز لمعالي الوزير". Optional, ≤256 chars.</summary>
    public string? GuestHintArabic { get; set; }
}

/// <summary>Admin layout edit. When <see cref="SeatCounts"/> is null/empty the
/// grid is UNIFORM: it writes <c>RowLabels.Count * SeatsPerRow</c> seats and is rejected
/// if that product exceeds <c>Hall.Capacity</c>. When <see cref="SeatCounts"/> is
/// non-empty it is AUTHORITATIVE (a per-row override) — its length MUST equal the row
/// count, each element is 1–80, and <c>sum(SeatCounts) &lt;= Hall.Capacity</c>; the stored
/// <see cref="SeatsPerRow"/> then keeps <c>max(SeatCounts)</c> as the uniform fallback.
/// Open for inheritance so the route-binding endpoint can carry a <c>HallId</c> field.</summary>
public class SetHallSeatLayoutRequest
{
    public IReadOnlyList<string> RowLabels { get; set; } = Array.Empty<string>();
    public int SeatsPerRow { get; set; }

    /// <summary>Optional per-row seat counts, parallel to <see cref="RowLabels"/>.
    /// Null/empty = uniform via <see cref="SeatsPerRow"/>. When non-empty its length must
    /// equal the row count and each value is 1–80.</summary>
    public IReadOnlyList<int>? SeatCounts { get; set; }

    /// <summary>The per-row seat TIERS, parallel to <see cref="RowLabels"/>.
    /// <list type="bullet">
    /// <item>Non-empty → AUTHORITATIVE: its length must equal the row count and each
    /// value must be a defined <see cref="SeatTier"/>.</item>
    /// <item>Null/empty on a hall that <b>already has</b> a layout → the stored tiers
    /// are kept unchanged (an old client cannot silently wipe them).</item>
    /// <item>Null/empty on a hall with <b>no</b> layout yet → every row defaults to
    /// <see cref="SeatTier.Vvip"/> (owner 2026-07-26: "default when defining seats at
    /// a session must be VVIP reserved"), i.e. a freshly defined hall is fully
    /// protocol-held until the admin downgrades rows.</item>
    /// </list></summary>
    public IReadOnlyList<SeatTier>? SeatTiers { get; set; }
}

/// <summary>Admin layout read-back. <see cref="SeatCounts"/> carries the
/// per-row seat counts when the layout is variable (null when uniform); the existing
/// <see cref="LayoutCapacity"/> is reused and simply carries <c>sum(SeatCounts)</c> in
/// the variable case.</summary>
public sealed record HallSeatLayoutSnapshot(
    Guid HallId,
    IReadOnlyList<string> RowLabels,
    int SeatsPerRow,
    int LayoutCapacity,
    int HallCapacity,
    IReadOnlyList<int>? SeatCounts = null,
    // Appended: the per-row seat tiers the CP editor reads back. Always one
    // entry per row (Normal for a legacy layout that carries no stored tiers).
    IReadOnlyList<SeatTier>? SeatTiers = null);

/// <summary>What the user sees after a successful reservation.
/// Returned by both self-pick and random-allocate. <see cref="Status"/>
/// is appended (append-only — the shipped app ignores unknown
/// JSON keys).
/// <para>The original approval queue is gone: a fresh
/// booking is created <c>Approved</c> (2026-07-18, reservation-only), so this
/// always returns <c>Approved</c>. The <c>Pending</c> parameter default is the
/// wire-safety value for a payload that omits the key, not a state the server
/// produces.</para></summary>
public sealed record MySeatReservation(
    Guid ReservationId,
    Guid SessionId,
    // Null for an OpenSeating join (general admission — no specific seat).
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    DateTime CreatedAt,
    BookingStatus Status = BookingStatus.Pending);

/// <summary>One row in the Control Panel booking
/// MONITOR: an ACTIVE (confirmed, still-held) visitor reservation. There is no
/// approval step (bookings auto-confirm), so this is a read-only monitor, not an
/// approval queue. Carries the session + seat + attendee. <see cref="AttendeeName"/>
/// is resolved from the Identity DB in a separate round-trip — there is no
/// cross-DB JOIN.</summary>
public sealed record ActiveBookingRow(
    Guid ReservationId,
    Guid SessionId,
    string SessionTitle,
    string SessionTitleArabic,
    DateTime SessionStart,
    // Null for an OpenSeating join — the CP renders it as "general admission".
    string? RowLabel,
    int? SeatNumber,
    SeatReservationKind Kind,
    Guid? AttendeeUserId,
    string AttendeeName,
    DateTime CreatedAt);

/// <summary>The staff seating desk's badge lookup:
/// resolve a scanned/typed badge QR id to where that guest is seated in this
/// session.</summary>
public class StaffSeatBadgeLookupRequest
{
    /// <summary>The 12-char badge QR id read off the guest's badge (or typed in
    /// the manual-entry field).</summary>
    public string QrId { get; set; } = string.Empty;
}

/// <summary>Who is in a seat (or where a scanned badge sits). One shape
/// serves both staff lookups so the tablet renders a single result card.
/// <para>Cross-DB safe: the occupant's name/photo are resolved from the
/// Identity DB in a separate round-trip and returned as plain values — no
/// cross-database join and nothing persisted into <c>SIMF_App</c>.</para></summary>
public sealed record StaffSeatOccupant(
    // False when the lookup found nothing: the seat is empty, or the scanned badge
    // holds no seat in this session. The other fields are then null/empty/default
    // and the tablet shows its "no seat" state.
    bool Found,
    Guid SessionId,
    // The seat's location — null when a badge lookup found no seat.
    string? RowLabel,
    int? SeatNumber,
    // The seat's tier, so the desk can say "this is a VVIP seat".
    SeatTier Tier,
    // The reservation id — the reference the desk reads back to the guest. Null
    // when the seat is free.
    Guid? ReservationId,
    SeatReservationKind Kind,
    BookingStatus Status,
    // The occupant's user id (logical FK to the Identity DB); null for an admin
    // block / VVIP protocol seat, which has no registration.
    Guid? UserId,
    // The occupant's display name, resolved from the Identity DB in a separate
    // round-trip. Empty for a VVIP protocol seat — read GuestHint instead.
    string DisplayName,
    string DisplayNameArabic,
    // The admin-typed VVIP guest hint (bilingual) — the occupant record for a seat
    // that has no registration.
    string? GuestHint,
    string? GuestHintArabic,
    // True when the occupant's avatar can be streamed from
    // GET /app/staff/sessions/{sessionId}/seating/occupant/{userId}/photo. The
    // tablet must fetch it through the authenticated Dio bytes path — a
    // bearer token cannot ride on a raw image URL.
    bool HasPhoto,
    // The guest's badge QR id, echoed so the desk can confirm the scan.
    string? QrId,
    // True when the holder has already checked in at the hall gate.
    bool CheckedIn);
