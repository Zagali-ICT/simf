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
/// <para>The public card's identity-card fields (name / logo / website /
/// contact / social / location) are sourced from the partner's own inlined
/// columns — superseding the removed shared <c>Contact</c> directory
/// (SIMF-FDS-014 / D-281). The JSON field names are unchanged, preserving the
/// shipped mobile/public wire contract (D-219).</para></summary>
internal sealed class PublicMediaPartnerService(SimfAppDbContext appDbContext)
    : IPublicMediaPartnerService
{
    public async Task<PublicMediaPartners> ListAsync(CancellationToken cancellationToken = default)
    {
        // One query — the card's identity-card fields come straight from the media
        // partner's own inlined columns (the shared Contact directory was removed,
        // so the prior second round-trip + dictionary coalesce is gone).
        var items = await appDbContext.MediaPartners.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.NameArabic)
            .Select(m => new PublicMediaPartnerItem(
                m.Id,
                m.Name,
                m.NameArabic,
                m.LogoRelativePath,
                m.Url,
                m.DisplayOrder,
                m.PhonePrimary,
                m.Email,
                m.FacebookUrl,
                m.XUrl,
                m.LinkedInUrl,
                m.InstagramUrl,
                m.Latitude,
                m.Longitude))
            .ToListAsync(cancellationToken);

        return new PublicMediaPartners(items);
    }
}
