using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Seeding;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-747 — verifies the by-hand 2026 content-seed SQL (docs/migrations/2026/*.sql)
/// applied by <see cref="SqlContentSeeder"/> lands the event content that used to
/// be seeded in C#, and that re-applying it is a no-op (each file is guarded by
/// IF NOT EXISTS). The test fixture already runs the roster files once in
/// <see cref="SimfApiFactory.EnsureDatabaseCreated"/>; these tests re-run them to
/// assert idempotency and inspect the result.
/// </summary>
public sealed class SqlContentSeederTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public SqlContentSeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Applies_the_programme_and_news_content_and_is_idempotent()
    {
        // Re-run the roster SQL (the fixture already ran it once) — must not
        // duplicate anything.
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // The one main hall (SIMF_App_Programme.sql).
        Assert.Equal(1, await db.Halls.CountAsync(h => h.Code == "MAIN"));

        // The three programme days (20-22 Nov 2026).
        var days = await db.ProgrammeDays
            .Where(d => d.Date >= new DateOnly(2026, 11, 20) && d.Date <= new DateOnly(2026, 11, 22))
            .Select(d => d.Date)
            .ToListAsync();
        Assert.Equal(3, days.Count);
        Assert.Contains(new DateOnly(2026, 11, 20), days);
        Assert.Contains(new DateOnly(2026, 11, 22), days);

        // The five placeholder sessions; the opening one carries the live URL.
        Assert.Equal(5, await db.Sessions.CountAsync(s => s.Code.StartsWith("S-D")));
        var opening = await db.Sessions.SingleAsync(s => s.Code == "S-D1-01");
        Assert.Equal("الجلسة الافتتاحية", opening.TitleArabic);
        Assert.False(string.IsNullOrWhiteSpace(opening.LiveStreamUrl));

        // The Highlights news item (SIMF_App_News.sql).
        Assert.Equal(1, await db.News.CountAsync(n => n.Category == "Highlights"));
    }

    [Fact]
    public async Task Applies_the_sponsors_media_archive_and_org_content()
    {
        // The fixture already applied the roster SQL in EnsureDatabaseCreated;
        // this test only asserts the resulting content (idempotency is covered
        // by the re-run in the programme/news test above).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Sponsors — the Platinum strategic sponsor + the ten total (SIMF_App_Sponsors.sql).
        Assert.True(await db.Sponsors.AnyAsync(s => s.Name == "Saudi Arabian Military Industries"));
        Assert.Equal(10, await db.Sponsors.CountAsync());

        // Media partners — three (SIMF_App_MediaPartners.sql).
        Assert.Equal(3, await db.MediaPartners.CountAsync());

        // Archive — the four past editions (2022-2025) + the 2024 child lists
        // (SIMF_App_Archive.sql).
        Assert.Equal(4, await db.ArchiveEditions.CountAsync(e => e.Year >= 2022 && e.Year <= 2025));
        var ed2024 = await db.ArchiveEditions.SingleAsync(e => e.Year == 2024);
        Assert.Equal(3, await db.ArchiveSessionTitles.CountAsync(t => t.ArchiveEditionId == ed2024.Id));
        Assert.Equal(5, await db.ArchivePastSpeakers.CountAsync(p => p.ArchiveEditionId == ed2024.Id));

        // Organisation about items — the four deck sections, Vision carrying the
        // real deck text (SIMF_App_Organization.sql upserts the D-495 placeholders).
        var about = await db.OrganizationAboutItems.OrderBy(i => i.DisplayOrder).ToListAsync();
        Assert.Equal(4, about.Count);
        Assert.Equal(
            new[] { "About the Forum", "Vision", "Mission", "Key Themes" },
            about.Select(i => i.Title).ToArray());
        Assert.Contains("رائدة عالمياً", about.Single(i => i.Title == "Vision").TextArabic);

        // Organisation social links were filled from empty.
        var org = await db.OrganizationProfile.SingleAsync();
        Assert.Equal("https://x.com/SIMF_RSNF", org.XUrl);
    }

    private async Task RunRosterAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SqlContentSeeder>()
            .RunAsync(SqlContentSeeder.RosterFiles, CancellationToken.None);
    }
}
