namespace SIMF.Contracts.Programme;

/// <summary>A speaker availability window as the
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

/// <summary>Create a speaker availability window.</summary>
public sealed class CreateSpeakerAvailabilityWindowRequest
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

/// <summary>One bookable slot derived from a speaker's windows: a
/// fixed-length time range that is not yet taken by an accepted meeting.</summary>
public sealed record SpeakerAvailableSlot(
    DateTime Start,
    DateTime End);

/// <summary>The forum's day boundary (MIN/MAX over the active
/// <c>ProgrammeDay.Date</c> rows) used by the CP to bound meeting-scheduling date
/// pickers to the event days. Both fields are <c>null</c> when no programme days
/// are seeded yet, in which case the CP applies no client-side date bound (the
/// server still enforces the rule once days exist).</summary>
public sealed record ForumWindowResponse(
    DateOnly? MinDate,
    DateOnly? MaxDate);
