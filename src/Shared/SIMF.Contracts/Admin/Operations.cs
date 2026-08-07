namespace SIMF.Contracts.Admin;

/// <summary>Current state of the registration gate, returned by
/// GET + admin endpoints.</summary>
public sealed record RegistrationGateState(
    bool IsOpen,
    DateTime? AutoClose,
    DateTime LastChangedAt,
    Guid? LastChangedByUserId);

/// <summary>Admin PUT body: toggle <c>IsOpen</c> and optionally
/// schedule the auto-close moment.</summary>
public sealed class UpdateRegistrationGateRequest
{
    public bool IsOpen { get; set; }
    public DateTime? AutoClose { get; set; }
}

/// <summary>Current state of the archive
/// visibility switch.</summary>
public sealed record ArchiveVisibilityState(
    bool IsVisible,
    DateTime LastChangedAt,
    Guid? LastChangedByUserId);

/// <summary>Admin PUT body for the archive visibility toggle.</summary>
public sealed class UpdateArchiveVisibilityRequest
{
    public bool IsVisible { get; set; }
}
