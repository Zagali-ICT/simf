// Tests: SIMF.Api.Tests/RegionTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Common;
using SIMF.Domain.Regions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Regions;

/// <summary>
/// Seeds the administrative-regions lookup from <see cref="SaudiRegions.All"/>
/// (the 13 official Saudi regions) so the app's region picker resolves on a
/// fresh database. Idempotent and keyed on <c>Code</c>: re-running only inserts
/// the rows that are missing — it never overwrites admin edits.
///
/// <para>Runs in <b>every</b> environment (regions are required reference data
/// the app depends on, unlike the dev-only <c>OrganisationSeeder</c>) and is
/// invoked explicitly by the host after migrations, mirroring
/// <c>RatingSeeder</c> / <c>IdentitySeeder</c>.</para>
/// </summary>
public sealed class RegionSeeder(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    ILogger<RegionSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await appDbContext.Regions
                .Select(region => region.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = timeProvider.GetUtcNow();
        var sortOrder = 0;
        var toAdd = new List<Region>();
        foreach (var source in SaudiRegions.All)
        {
            var order = sortOrder++;
            if (existing.Contains(source.Code))
            {
                continue;
            }
            toAdd.Add(new Region
            {
                Id = Guid.NewGuid(),
                Code = source.Code,
                NameArabic = source.Arabic,
                Name = source.English,
                SortOrder = order,
                IsActive = true,
                CreatedAt = now,
            });
        }

        if (toAdd.Count == 0)
        {
            logger.LogInformation(
                "Region seed skipped — all {Count} regions already present.",
                SaudiRegions.All.Count);
            return;
        }

        appDbContext.Regions.AddRange(toAdd);
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Region seed inserted {Count} region(s).", toAdd.Count);
    }
}
