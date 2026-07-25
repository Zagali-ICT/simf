namespace SIMF.Contracts.Admin;

/// <summary>D-166 (gap doc G4, PDF §2.3) — current state of the
/// registration gate, returned by GET + admin endpoints.</summary>
public sealed record RegistrationGateState(
    bool IsOpen,
    DateTimeOffset? AutoClose,
    DateTimeOffset LastChangedAt,
    Guid? LastChangedByUserId);

/// <summary>D-166 — admin PUT body: toggle <c>IsOpen</c> and optionally
/// schedule the auto-close moment.</summary>
public sealed class UpdateRegistrationGateRequest
{
    public bool IsOpen { get; set; }
    public DateTimeOffset? AutoClose { get; set; }
}

/// <summary>D-166 (gap doc G4, PDF §2.4) — current state of the archive
/// visibility switch.</summary>
public sealed record ArchiveVisibilityState(
    bool IsVisible,
    DateTimeOffset LastChangedAt,
    Guid? LastChangedByUserId);

/// <summary>D-166 — admin PUT body for the archive visibility toggle.</summary>
public sealed class UpdateArchiveVisibilityRequest
{
    public bool IsVisible { get; set; }
}
