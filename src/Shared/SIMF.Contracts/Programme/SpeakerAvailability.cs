namespace SIMF.Contracts.Programme;

/// <summary>D-474 (#11, Group G phase 1) — a speaker availability window as the
/// admin sees it (the team defines these; the VIP-meeting flow offers their free
/// slots).</summary>
public sealed record AdminSpeakerAvailabilityWindow(
    Guid Id,
    Guid SpeakerId,
    DateTime Start,
    DateTime End,
    int SlotMinutes,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>D-474 — create a speaker availability window.</summary>
public sealed class CreateSpeakerAvailabilityWindowRequest
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

/// <summary>D-474 — one bookable slot derived from a speaker's windows: a
/// fixed-length time range that is not yet taken by an accepted meeting.</summary>
public sealed record SpeakerAvailableSlot(
    DateTime Start,
    DateTime End);

/// <summary>D-753 — the forum's day boundary (MIN/MAX over the active
/// <c>ProgrammeDay.Date</c> rows) used by the CP to bound meeting-scheduling date
/// pickers to the event days. Both fields are <c>null</c> when no programme days
/// are seeded yet, in which case the CP applies no client-side date bound (the
/// server still enforces the rule once days exist).</summary>
public sealed record ForumWindowResponse(
    DateOnly? MinDate,
    DateOnly? MaxDate);
