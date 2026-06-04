using SIMF.Contracts.Admin;

namespace SIMF.Application.Operations.Abstractions;

/// <summary>D-166 (gap doc G4) — read + admin-write surface for the
/// two singleton operations toggles (RegistrationGate + ArchiveVisibility).</summary>
public interface IOperationsToggleService
{
    Task<RegistrationGateState> GetRegistrationGateAsync(
        CancellationToken cancellationToken = default);

    Task<RegistrationGateState> UpdateRegistrationGateAsync(
        Guid actorUserId, UpdateRegistrationGateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true when sign-up should be accepted right now —
    /// IsOpen is true AND (no AutoCloseUtc OR AutoCloseUtc is in the
    /// future). Called by the sign-up endpoint on every request.</summary>
    Task<bool> IsRegistrationOpenAsync(CancellationToken cancellationToken = default);

    Task<ArchiveVisibilityState> GetArchiveVisibilityAsync(
        CancellationToken cancellationToken = default);

    Task<ArchiveVisibilityState> UpdateArchiveVisibilityAsync(
        Guid actorUserId, UpdateArchiveVisibilityRequest request,
        CancellationToken cancellationToken = default);
}
