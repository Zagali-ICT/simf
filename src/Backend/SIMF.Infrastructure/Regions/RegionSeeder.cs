// Tests: SIMF.Api.Tests/RegionTests.cs
using Microsoft.Data.SqlClient;
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
/// the app depends on) and is invoked explicitly by the host after migrations,
/// mirroring <c>RatingSeeder</c> / <c>IdentitySeeder</c>.</para>
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

        var now = timeProvider.SimfNow();
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
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
        {
            // Seeding runs on EVERY API instance at startup and is not held under
            // the worker lease, so on the first boot of a fresh database several
            // instances read zero rows and insert the same 13 codes. The unique
            // index on Code rejects the losers — which means the rows this seeder
            // exists to guarantee are now present, i.e. exactly the outcome it
            // promises. Detaching and returning keeps the losing node booting
            // instead of failing startup on work that in fact succeeded.
            foreach (var region in toAdd)
            {
                appDbContext.Entry(region).State = EntityState.Detached;
            }
            logger.LogInformation(
                "Region seed lost the first-boot race — another instance inserted the {Count} region(s).",
                toAdd.Count);
            return;
        }

        logger.LogInformation(
            "Region seed inserted {Count} region(s).", toAdd.Count);
    }

    /// <summary>True only when the store rejected the write on a UNIQUE index —
    /// SQL Server 2601 (duplicate key in a unique index) or 2627 (unique
    /// constraint). Every other <see cref="DbUpdateException"/> must propagate;
    /// swallowing them would hide a real seed failure behind a silent success.</summary>
    private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
