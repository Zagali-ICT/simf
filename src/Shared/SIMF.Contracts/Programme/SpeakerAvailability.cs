namespace SIMF.Contracts.Programme;

/// <summary>D-474 (#11, Group G phase 1) — a speaker availability window as the
/// admin sees it (the team defines these; the VIP-meeting flow offers their free
/// slots).</summary>
public sealed record AdminSpeakerAvailabilityWindow(
    Guid Id,
    Guid SpeakerId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int SlotMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-474 — create a speaker availability window.</summary>
public sealed class CreateSpeakerAvailabilityWindowRequest
{
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

/// <summary>D-474 — one bookable slot derived from a speaker's windows: a
/// fixed-length time range that is not yet taken by an accepted meeting.</summary>
public sealed record SpeakerAvailableSlot(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);
