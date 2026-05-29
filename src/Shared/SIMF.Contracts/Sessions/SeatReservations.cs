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
    int ActiveReservedCount);

/// <summary>D-175 — one occupied seat in the grid.</summary>
public sealed record SessionSeatCell(
    Guid ReservationId,
    string RowLabel,
    int SeatNumber,
    SeatReservationKind Kind);

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
/// Returned by both self-pick and random-allocate.</summary>
public sealed record MySeatReservation(
    Guid ReservationId,
    Guid SessionId,
    string RowLabel,
    int SeatNumber,
    SeatReservationKind Kind,
    DateTimeOffset CreatedAt);
