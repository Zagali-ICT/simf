using SIMF.Contracts.Delegations;

namespace SIMF.Application.Delegations.Abstractions;

/// <summary>D-174 — anonymous public list of active delegations
/// (Mockup page 21 mobile-app screen).</summary>
public interface IPublicDelegationService
{
    Task<PublicDelegations> ListAsync(
        CancellationToken cancellationToken = default);
}
