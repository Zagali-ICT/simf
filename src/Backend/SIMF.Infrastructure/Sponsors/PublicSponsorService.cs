// Tests: SIMF.Api.Tests/SponsorsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Contracts.Sponsors;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Sponsors;

/// <summary>D-199 (Mockup page 23) — anonymous public list of active sponsors,
/// grouped by tier (highest tier first; Platinum=10 before Bronze=40), then by
/// DisplayOrder then NameArabic. Mirrors PublicDelegationService.
///
/// <para>SIMF-FDS-014 (D-281) — when a sponsor links a shared <c>Contact</c>,
/// the public card's name / logo / website are sourced from that Contact
/// (falling back to the sponsor's own inline columns). The JSON field names are
/// unchanged, preserving the shipped mobile/public wire contract (D-219).</para></summary>
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
            .Select(sponsor => new
            {
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
                sponsor.Tier,
                sponsor.LogoRelativePath,
                sponsor.Url,
                sponsor.DisplayOrder,
                sponsor.ContactId,
                // D-432 — the tagline is sponsor-owned (not on Contact).
                sponsor.Tagline,
                sponsor.TaglineArabic,
            })
            .ToListAsync(cancellationToken);

        // SIMF-FDS-014 (D-281) — batch-resolve the linked active Contacts in one
        // query (mirrors the country batch-resolve in AdminSpeakerService), then
        // coalesce the card fields below.
        var contactIds = rows
            .Where(r => r.ContactId.HasValue)
            .Select(r => r.ContactId!.Value)
            .Distinct()
            .ToList();
        var contactsById = contactIds.Count == 0
            ? new Dictionary<Guid, ContactCard>()
            : await appDbContext.Contacts.AsNoTracking()
                .Where(contact => contactIds.Contains(contact.Id) && contact.IsActive)
                .Select(contact => new ContactCard(
                    contact.Id, contact.Name, contact.NameArabic,
                    contact.LogoRelativePath, contact.Website,
                    contact.PhonePrimary, contact.Email,
                    contact.FacebookUrl, contact.XUrl, contact.LinkedInUrl, contact.InstagramUrl,
                    contact.Latitude, contact.Longitude, contact.CountryId))
                .ToDictionaryAsync(card => card.Id, cancellationToken);

        // D-456 — batch-resolve the linked Contacts' country names in one query
        // (the Country lookup has no nav property — mirror the Speaker pattern).
        var countryIds = contactsById.Values
            .Where(c => c.CountryId.HasValue)
            .Select(c => c.CountryId!.Value)
            .Distinct()
            .ToList();
        var countriesById = countryIds.Count == 0
            ? new Dictionary<int, (string En, string Ar)>()
            : await appDbContext.Countries.AsNoTracking()
                .Where(country => countryIds.Contains(country.Id))
                .Select(country => new { country.Id, country.Name, country.NameArabic })
                .ToDictionaryAsync(
                    country => country.Id,
                    country => (En: country.Name, Ar: country.NameArabic),
                    cancellationToken);

        var groups = rows
            .Select(r =>
            {
                var c = r.ContactId is { } cid ? contactsById.GetValueOrDefault(cid) : null;
                var country = c?.CountryId is { } cnid
                    ? countriesById.GetValueOrDefault(cnid)
                    : default;
                return new PublicSponsor(
                    r.Id,
                    c?.Name ?? r.Name,
                    c?.NameArabic ?? r.NameArabic,
                    (int)r.Tier,
                    r.Tier.ToString(),
                    c?.LogoRelativePath ?? r.LogoRelativePath,
                    c?.Website ?? r.Url,
                    r.DisplayOrder,
                    c?.PhonePrimary,
                    c?.Email,
                    c?.FacebookUrl,
                    c?.XUrl,
                    c?.LinkedInUrl,
                    c?.InstagramUrl,
                    c?.Latitude,
                    c?.Longitude,
                    r.Tagline,
                    r.TaglineArabic,
                    c?.CountryId,
                    country.En,
                    country.Ar);
            })
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
        var row = await appDbContext.Sponsors.AsNoTracking()
            .Where(sponsor => sponsor.IsActive && sponsor.Id == id)
            .Select(sponsor => new
            {
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
                sponsor.Tier,
                sponsor.LogoRelativePath,
                sponsor.Url,
                sponsor.ContactId,
                // Wave 3 — the about is sponsor-owned (like the tagline).
                sponsor.About,
                sponsor.AboutArabic,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // SIMF-FDS-014 (D-281) — coalesce the card's name / logo / website over the
        // linked active Contact; Wave 3 adds the city + country from that Contact.
        var contact = row.ContactId is { } cid
            ? await appDbContext.Contacts.AsNoTracking()
                .Where(c => c.Id == cid && c.IsActive)
                .Select(c => new
                {
                    c.Name,
                    c.NameArabic,
                    c.LogoRelativePath,
                    c.Website,
                    c.City,
                    c.CityArabic,
                    c.CountryId,
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        (string En, string Ar)? country = contact?.CountryId is { } cnid
            ? await appDbContext.Countries.AsNoTracking()
                .Where(c => c.Id == cnid)
                .Select(c => new ValueTuple<string, string>(c.Name, c.NameArabic))
                .Cast<(string En, string Ar)?>()
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new PublicSponsorDetail(
            row.Id,
            contact?.Name ?? row.Name,
            contact?.NameArabic ?? row.NameArabic,
            (int)row.Tier,
            row.Tier.ToString(),
            contact?.LogoRelativePath ?? row.LogoRelativePath,
            contact?.Website ?? row.Url,
            row.About,
            row.AboutArabic,
            contact?.City,
            contact?.CityArabic,
            contact?.CountryId,
            country?.En,
            country?.Ar);
    }

    /// <summary>SIMF-FDS-014 (D-281) — the linked-Contact fields the public
    /// sponsor card coalesces over its own inline columns.</summary>
    private sealed record ContactCard(
        Guid Id, string? Name, string NameArabic,
        string? LogoRelativePath, string? Website,
        string? PhonePrimary, string? Email,
        string? FacebookUrl, string? XUrl, string? LinkedInUrl, string? InstagramUrl,
        double? Latitude, double? Longitude, int? CountryId);
}
