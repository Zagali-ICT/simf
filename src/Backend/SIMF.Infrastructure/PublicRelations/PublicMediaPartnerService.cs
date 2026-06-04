// Tests: SIMF.Api.Tests/MediaPartnersTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Contracts.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>D-199 (Mockup page 31) — read-only public projection of active
/// media partners for the mobile app + website. The service just returns
/// active rows ordered by (DisplayOrder asc, NameArabic asc). Mirrors
/// <c>PublicDelegationService</c>.
///
/// <para>SIMF-FDS-014 (D-281) — when a partner links a shared <c>Contact</c>,
/// the public card's name / logo / website are sourced from that Contact
/// (falling back to the partner's own inline columns). The JSON field names are
/// unchanged, preserving the shipped mobile/public wire contract (D-219).</para></summary>
internal sealed class PublicMediaPartnerService(SimfAppDbContext appDbContext)
    : IPublicMediaPartnerService
{
    public async Task<PublicMediaPartners> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.MediaPartners.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.NameArabic)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.NameArabic,
                m.LogoRelativePath,
                m.Url,
                m.DisplayOrder,
                m.ContactId,
            })
            .ToListAsync(cancellationToken);

        // SIMF-FDS-014 (D-281) — batch-resolve the linked active Contacts, then
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

        var items = rows
            .Select(r =>
            {
                var c = r.ContactId is { } cid ? contactsById.GetValueOrDefault(cid) : null;
                return new PublicMediaPartnerItem(
                    r.Id,
                    c?.Name ?? r.Name,
                    c?.NameArabic ?? r.NameArabic,
                    c?.LogoRelativePath ?? r.LogoRelativePath,
                    c?.Website ?? r.Url,
                    r.DisplayOrder);
            })
            .ToList();

        return new PublicMediaPartners(items);
    }

    /// <summary>SIMF-FDS-014 (D-281) — the linked-Contact fields the public
    /// media-partner card coalesces over its own inline columns.</summary>
    private sealed record ContactCard(
        Guid Id, string? Name, string NameArabic,
        string? LogoRelativePath, string? Website);
}
