// Tests: SIMF.Api.Tests/MediaPartnersTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Contracts.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>D-199 (Mockup page 31) — read-only public projection of active
/// media partners for the mobile app + website. The service just returns
/// active rows ordered by (DisplayOrder asc, NameArabic asc). Mirrors
/// <c>PublicDelegationService</c>.</summary>
internal sealed class PublicMediaPartnerService(SimfAppDbContext appDbContext)
    : IPublicMediaPartnerService
{
    public async Task<PublicMediaPartners> ListAsync(CancellationToken cancellationToken = default)
    {
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
                m.DisplayOrder))
            .ToListAsync(cancellationToken);

        return new PublicMediaPartners(items);
    }
}
