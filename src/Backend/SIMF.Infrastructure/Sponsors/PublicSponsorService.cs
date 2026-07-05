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
        // A5 — one query with the Sponsor→Contact→Country LEFT JOINs replaces the
        // prior three round-trips (sponsors, then a batch of active Contacts, then
        // a batch of Countries). The linked Contact is surfaced only when it is
        // active (the guard mirrors the old `contact.IsActive` batch filter); each
        // contact-derived value is projected as null when the contact is missing
        // or soft-deleted, so the in-memory coalesce below is exactly the old
        // `c?.Field ?? sponsor.Field` semantics.
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
                // D-432 — the tagline is sponsor-owned (not on Contact).
                sponsor.Tagline,
                sponsor.TaglineArabic,
                ContactName = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Name : null,
                ContactNameArabic = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.NameArabic : null,
                ContactLogoRelativePath = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.LogoRelativePath : null,
                ContactWebsite = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Website : null,
                ContactPhonePrimary = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.PhonePrimary : null,
                ContactEmail = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Email : null,
                ContactFacebookUrl = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.FacebookUrl : null,
                ContactXUrl = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.XUrl : null,
                ContactLinkedInUrl = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.LinkedInUrl : null,
                ContactInstagramUrl = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.InstagramUrl : null,
                ContactLatitude = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Latitude : null,
                ContactLongitude = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Longitude : null,
                ContactCountryId = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.CountryId : null,
                ContactCountryNameEn = sponsor.Contact != null && sponsor.Contact.IsActive
                    && sponsor.Contact.Country != null ? sponsor.Contact.Country.Name : null,
                ContactCountryNameAr = sponsor.Contact != null && sponsor.Contact.IsActive
                    && sponsor.Contact.Country != null ? sponsor.Contact.Country.NameArabic : null,
            })
            .ToListAsync(cancellationToken);

        var groups = rows
            .Select(r => new PublicSponsor(
                r.Id,
                r.ContactName ?? r.Name,
                r.ContactNameArabic ?? r.NameArabic,
                (int)r.Tier,
                r.Tier.ToString(),
                r.ContactLogoRelativePath ?? r.LogoRelativePath,
                r.ContactWebsite ?? r.Url,
                r.DisplayOrder,
                r.ContactPhonePrimary,
                r.ContactEmail,
                r.ContactFacebookUrl,
                r.ContactXUrl,
                r.ContactLinkedInUrl,
                r.ContactInstagramUrl,
                r.ContactLatitude,
                r.ContactLongitude,
                r.Tagline,
                r.TaglineArabic,
                r.ContactCountryId,
                r.ContactCountryNameEn,
                r.ContactCountryNameAr))
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
        // A5 — one query with the Sponsor→Contact→Country LEFT JOINs (was three
        // round-trips). The linked Contact is surfaced only when active; its name /
        // logo / website fall back to the sponsor's inline columns, and the city +
        // country come from that Contact — exactly the old coalesce semantics.
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
                // Wave 3 — the about is sponsor-owned (like the tagline).
                sponsor.About,
                sponsor.AboutArabic,
                ContactName = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Name : null,
                ContactNameArabic = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.NameArabic : null,
                ContactLogoRelativePath = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.LogoRelativePath : null,
                ContactWebsite = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.Website : null,
                ContactCity = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.City : null,
                ContactCityArabic = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.CityArabic : null,
                ContactCountryId = sponsor.Contact != null && sponsor.Contact.IsActive
                    ? sponsor.Contact.CountryId : null,
                ContactCountryNameEn = sponsor.Contact != null && sponsor.Contact.IsActive
                    && sponsor.Contact.Country != null ? sponsor.Contact.Country.Name : null,
                ContactCountryNameAr = sponsor.Contact != null && sponsor.Contact.IsActive
                    && sponsor.Contact.Country != null ? sponsor.Contact.Country.NameArabic : null,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return new PublicSponsorDetail(
            row.Id,
            row.ContactName ?? row.Name,
            row.ContactNameArabic ?? row.NameArabic,
            (int)row.Tier,
            row.Tier.ToString(),
            row.ContactLogoRelativePath ?? row.LogoRelativePath,
            row.ContactWebsite ?? row.Url,
            row.About,
            row.AboutArabic,
            row.ContactCity,
            row.ContactCityArabic,
            row.ContactCountryId,
            row.ContactCountryNameEn,
            row.ContactCountryNameAr);
    }
}
