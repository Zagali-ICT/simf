using SIMF.Contracts.Admin;

namespace SIMF.Application.Operations.Abstractions;

/// <summary>Read + admin-write surface for the
/// two singleton operations toggles (RegistrationGate + ArchiveVisibility).</summary>
public interface IOperationsToggleService
{
    Task<RegistrationGateState> GetRegistrationGateAsync(
        CancellationToken cancellationToken = default);

    Task<RegistrationGateState> UpdateRegistrationGateAsync(
        Guid actorUserId, UpdateRegistrationGateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true when sign-up should be accepted right now —
    /// IsOpen is true AND (no AutoClose OR AutoClose is in the
    /// future). Called by the sign-up endpoint on every request.</summary>
    Task<bool> IsRegistrationOpenAsync(CancellationToken cancellationToken = default);

    Task<ArchiveVisibilityState> GetArchiveVisibilityAsync(
        CancellationToken cancellationToken = default);

    Task<ArchiveVisibilityState> UpdateArchiveVisibilityAsync(
        Guid actorUserId, UpdateArchiveVisibilityRequest request,
        CancellationToken cancellationToken = default);
}
