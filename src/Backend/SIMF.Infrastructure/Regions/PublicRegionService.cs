// Tests: SIMF.Api.Tests/RegionTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Regions.Abstractions;
using SIMF.Contracts.Regions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Regions;

/// <summary>
/// Region lookup — public, read-only picker over the active regions directory.
/// Mirrors <c>PublicOrganisationService</c>: AsNoTracking, IsActive filter,
/// projection to the public contract. Ordered by SortOrder then Arabic name.
/// </summary>
internal sealed class PublicRegionService(SimfAppDbContext db) : IPublicRegionService
{
    public async Task<IReadOnlyList<RegionPickerItem>> GetActiveRegionsAsync(
        CancellationToken ct = default) =>
        await db.Regions
            .AsNoTracking()
            .Where(region => region.IsActive)
            .OrderBy(region => region.SortOrder)
            .ThenBy(region => region.NameArabic)
            .Select(region => new RegionPickerItem(
                region.Code,
                region.Name,
                region.NameArabic))
            .ToListAsync(ct);
}
