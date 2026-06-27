using SIMF.Contracts.Delegations;

namespace SIMF.Application.Delegations.Abstractions;

/// <summary>D-499 (Figma 1426:10771 الوفود) — anonymous public delegations view:
/// the invited countries grouped with their head, dates and member count.</summary>
public interface IPublicDelegationService
{
    Task<AppDelegations> GetAsync(CancellationToken cancellationToken = default);
}
