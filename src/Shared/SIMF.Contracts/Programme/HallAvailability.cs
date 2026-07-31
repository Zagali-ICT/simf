namespace SIMF.Contracts.Programme;

/// <summary>D-715 (item 7, FDS-013 §15 GAP-1) — a hall availability window as the
/// admin sees it (the team defines these; the meeting-review flow binds an
/// accepted request to a free hall slot). Symmetric with
/// <see cref="AdminSpeakerAvailabilityWindow"/>.</summary>
public sealed record AdminHallAvailabilityWindow(
    Guid Id,
    Guid HallId,
    DateTime Start,
    DateTime End,
    int SlotMinutes,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>D-715 — create a hall availability window.</summary>
public sealed class CreateHallAvailabilityWindowRequest
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

/// <summary>D-715 — one bookable slot derived from a hall's windows: a
/// fixed-length time range that is not yet taken by a bound meeting.</summary>
public sealed record HallAvailableSlot(
    DateTime Start,
    DateTime End);
