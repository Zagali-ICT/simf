namespace SIMF.Common.Enums;

/// <summary>D-485 (owner batch 2026-06-21) — how attendees join a session held in
/// a given <c>Hall</c> (or overridden per <c>Session</c>):
/// <list type="bullet">
/// <item><see cref="AssignedSeat"/> — the attendee picks a specific seat from the
/// hall's seat layout (a seat-specific <c>SeatReservation</c>, the pre-existing
/// behaviour). Requires a configured layout.</item>
/// <item><see cref="OpenSeating"/> — the attendee just joins (bulk / general
/// admission); no seat is chosen and the reservation carries a null row/seat.</item>
/// </list>
/// The effective mode for a session is <c>Session.SeatSelectionModeOverride ??
/// Hall.SeatSelectionMode</c>. Stored as int; <see cref="AssignedSeat"/> = 0 keeps
/// every pre-existing hall on the old pick-a-seat behaviour.</summary>
public enum SeatSelectionMode
{
    AssignedSeat = 0,
    OpenSeating = 1,
}
