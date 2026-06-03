// Tests: SIMF.Api.Tests/SponsorsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Contracts.Sponsors;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Sponsors;

/// <summary>D-199 (Mockup page 23) — anonymous public list of active sponsors,
/// grouped by tier (highest tier first; Platinum=10 before Bronze=40), then by
/// DisplayOrder then NameArabic. Mirrors PublicDelegationService.</summary>
internal sealed class PublicSponsorService(SimfAppDbContext appDbContext)
    : IPublicSponsorService
{
    public async Task<PublicSponsors> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.Sponsors.AsNoTracking()
            .Where(sponsor => sponsor.IsActive)
            .OrderBy(sponsor => sponsor.Tier)
            .ThenBy(sponsor => sponsor.DisplayOrder)
            .ThenBy(sponsor => sponsor.NameArabic)
            .Select(sponsor => new PublicSponsor(
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
                (int)sponsor.Tier,
                sponsor.Tier.ToString(),
                sponsor.LogoRelativePath,
                sponsor.Url,
                sponsor.DisplayOrder))
            .ToListAsync(cancellationToken);

        var groups = rows
            .GroupBy(sponsor => new { sponsor.Tier, sponsor.TierName })
            .OrderBy(group => group.Key.Tier)
            .Select(group => new PublicSponsorTierGroup(
                group.Key.Tier,
                group.Key.TierName,
                group.ToList()))
            .ToList();

        return new PublicSponsors(groups);
    }
}
