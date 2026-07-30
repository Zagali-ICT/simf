using SIMF.Contracts.Delegations;

namespace SIMF.Application.Delegations.Abstractions;

/// <summary>D-499 (Figma 1426:10771 الوفود) — anonymous public delegations view:
/// the invited countries grouped with their head, dates and member count.</summary>
public interface IPublicDelegationService
{
    /// <summary>Builds the delegations view for one viewer. G2 (D-800) —
    /// <c>viewerUserId</c> is the signed-in caller's user id, or <c>null</c> for an
    /// anonymous caller. When supplied, the country matching that caller's
    /// <c>UserProfile.NationalityId</c> is left out of the list and of the two
    /// aggregate stats, so a viewer never sees their own delegation. An anonymous
    /// caller — and a caller with no profile row — gets the full list.</summary>
    Task<AppDelegations> GetAsync(
        Guid? viewerUserId, CancellationToken cancellationToken = default);
}
