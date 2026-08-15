// Tests: SIMF.Api.Tests/SponsorsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Sponsors;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Sponsors;

/// <summary>Anonymous public list of active sponsors,
/// grouped by tier (highest tier first; Platinum=10 before Bronze=40), then by
/// DisplayOrder then NameArabic. Mirrors PublicDelegationService.
///
/// <para>The public card's identity-card fields (name / logo / website /
/// contact / social / location) are sourced from the sponsor's own inlined
/// columns — superseding the removed shared <c>Contact</c>
/// directory. The JSON field names are unchanged, preserving the
/// shipped mobile/public wire contract.</para></summary>
internal sealed class PublicSponsorService(
    SimfAppDbContext appDbContext,
    IAssetService assetService) : IPublicSponsorService
{
    public async Task<PublicSponsors> ListAsync(
        CancellationToken cancellationToken = default)
    {
        // A5 — one query with a Sponsor→Country LEFT JOIN. The identity-card
        // fields come straight from the sponsor's own inlined columns (the shared
        // Contact directory was removed); the country name is read through the
        // join and is null when no country is set.
        var rows = await appDbContext.Sponsors.AsNoTracking()
            .Where(sponsor => sponsor.IsActive)
            .OrderBy(sponsor => sponsor.Tier)
            .ThenBy(sponsor => sponsor.DisplayOrder)
            .ThenBy(sponsor => sponsor.NameArabic)
            .Select(sponsor => new
            {
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
                sponsor.Tier,
                sponsor.Url,
                sponsor.DisplayOrder,
                // The tagline is sponsor-owned.
                sponsor.Tagline,
                sponsor.TaglineArabic,
                sponsor.PhonePrimary,
                sponsor.Email,
                sponsor.FacebookUrl,
                sponsor.XUrl,
                sponsor.LinkedInUrl,
                sponsor.InstagramUrl,
                sponsor.Latitude,
                sponsor.Longitude,
                sponsor.CountryId,
                CountryNameEn = sponsor.Country != null ? sponsor.Country.Name : null,
                CountryNameAr = sponsor.Country != null ? sponsor.Country.NameArabic : null,
            })
            .ToListAsync(cancellationToken);

        // Which sponsors actually have a logo in the store — one batched query
        // for the whole list, no N+1. The wire's LogoRelativePath is retained but
        // always null, so HasLogo is what a client branches on.
        var withLogo = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.SponsorLogo,
            rows.Select(r => r.Id).ToList(),
            cancellationToken);

        var groups = rows
            .Select(r => new PublicSponsor(
                r.Id,
                r.Name,
                r.NameArabic,
                (int)r.Tier,
                r.Tier.ToString(),
                // The logo is a StoredFile; the client builds the URL from the id.
                null,
                r.Url,
                r.DisplayOrder,
                r.PhonePrimary,
                r.Email,
                r.FacebookUrl,
                r.XUrl,
                r.LinkedInUrl,
                r.InstagramUrl,
                r.Latitude,
                r.Longitude,
                r.Tagline,
                r.TaglineArabic,
                r.CountryId,
                r.CountryNameEn,
                r.CountryNameAr,
                withLogo.Contains(r.Id)))
            .GroupBy(sponsor => new { sponsor.Tier, sponsor.TierName })
            .OrderBy(group => group.Key.Tier)
            .Select(group => new PublicSponsorTierGroup(
                group.Key.Tier,
                group.Key.TierName,
                group.ToList()))
            .ToList();

        return new PublicSponsors(groups);
    }

    public async Task<PublicSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // A5 — one query with a Sponsor→Country LEFT JOIN. The identity-card
        // fields (name / logo / website / city) come straight from the sponsor's
        // own inlined columns (the shared Contact directory was removed); the
        // country name is read through the join.
        var row = await appDbContext.Sponsors.AsNoTracking()
            .Where(sponsor => sponsor.IsActive && sponsor.Id == id)
            .Select(sponsor => new
            {
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
                sponsor.Tier,
                sponsor.Url,
                // The about is sponsor-owned (like the tagline).
                sponsor.About,
                sponsor.AboutArabic,
                sponsor.City,
                sponsor.CityArabic,
                sponsor.CountryId,
                CountryNameEn = sponsor.Country != null ? sponsor.Country.Name : null,
                CountryNameAr = sponsor.Country != null ? sponsor.Country.NameArabic : null,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return new PublicSponsorDetail(
            row.Id,
            row.Name,
            row.NameArabic,
            (int)row.Tier,
            row.Tier.ToString(),
            // The logo is a StoredFile; the client builds the URL from the id.
            null,
            row.Url,
            row.About,
            row.AboutArabic,
            row.City,
            row.CityArabic,
            row.CountryId,
            row.CountryNameEn,
            row.CountryNameAr);
    }
}
