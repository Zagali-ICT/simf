using SIMF.Contracts.Sponsors;

namespace SIMF.Application.Sponsors.Abstractions;

/// <summary>Anonymous public list of active sponsors,
/// grouped by tier (highest first).</summary>
public interface IPublicSponsorService
{
    Task<PublicSponsors> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>The full sponsor-detail view (about,
    /// city, website, tier, country). Null when the sponsor is missing / inactive.</summary>
    Task<PublicSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
