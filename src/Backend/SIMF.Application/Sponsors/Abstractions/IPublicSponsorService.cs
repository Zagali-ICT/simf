using SIMF.Contracts.Sponsors;

namespace SIMF.Application.Sponsors.Abstractions;

/// <summary>D-199 (Mockup page 23) — anonymous public list of active sponsors,
/// grouped by tier (highest first).</summary>
public interface IPublicSponsorService
{
    Task<PublicSponsors> ListAsync(
        CancellationToken cancellationToken = default);
}
