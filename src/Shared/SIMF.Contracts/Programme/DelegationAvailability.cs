namespace SIMF.Contracts.Programme;

/// <summary>Bi-Meeting rework — a delegation availability window as the admin sees
/// it (the team defines these per delegation/country; the delegation-meeting flow
/// offers their free slots). Parity with <see cref="AdminSpeakerAvailabilityWindow"/>.</summary>
public sealed record AdminDelegationAvailabilityWindow(
    Guid Id,
    int CountryId,
    DateTimeOffset Start,
    DateTimeOffset End,
    int SlotMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Bi-Meeting rework — create a delegation availability window.</summary>
public sealed class CreateDelegationAvailabilityWindowRequest
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

/// <summary>Bi-Meeting rework — one bookable slot derived from a delegation's
/// windows: a fixed-length time range not yet taken by a live delegation meeting.</summary>
public sealed record DelegationAvailableSlot(
    DateTimeOffset Start,
    DateTimeOffset End);
