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
                    contact.LogoRelativePath, contact.Website))
                .ToDictionaryAsync(card => card.Id, cancellationToken);

        var groups = rows
            .Select(r =>
            {
                var c = r.ContactId is { } cid ? contactsById.GetValueOrDefault(cid) : null;
                return new PublicSponsor(
                    r.Id,
                    c?.Name ?? r.Name,
                    c?.NameArabic ?? r.NameArabic,
                    (int)r.Tier,
                    r.Tier.ToString(),
                    c?.LogoRelativePath ?? r.LogoRelativePath,
                    c?.Website ?? r.Url,
                    r.DisplayOrder);
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

    /// <summary>SIMF-FDS-014 (D-281) — the linked-Contact fields the public
    /// sponsor card coalesces over its own inline columns.</summary>
    private sealed record ContactCard(
        Guid Id, string? Name, string NameArabic,
        string? LogoRelativePath, string? Website);
}
