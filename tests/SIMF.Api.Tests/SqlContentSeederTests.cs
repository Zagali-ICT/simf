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
[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
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

        // The three programme days. These were 20-22 Nov 2026 while the seed
        // carried placeholder content; db9b6f76 (2026-07-21) replaced it with the
        // REAL SIMF-4 programme on 23-25 Nov and soft-deletes the placeholder
        // days by (Date, Title) in the same file, so 20-22 must now be gone.
        var days = await db.ProgrammeDays
            .Where(d => d.IsActive
                && d.Date >= new DateOnly(2026, 11, 23) && d.Date <= new DateOnly(2026, 11, 25))
            .Select(d => d.Date)
            .ToListAsync();
        Assert.Equal(3, days.Count);
        Assert.Contains(new DateOnly(2026, 11, 23), days);
        Assert.Contains(new DateOnly(2026, 11, 25), days);

        // The guarded cleanup half of that swap: no active placeholder day survives.
        Assert.Empty(await db.ProgrammeDays
            .Where(d => d.IsActive
                && d.Date >= new DateOnly(2026, 11, 20) && d.Date <= new DateOnly(2026, 11, 22))
            .ToListAsync());

        // The real programme: 59 sessions coded D{day}-{nn}, and none of the five
        // retired S-D* placeholders still active.
        Assert.Equal(59, await db.Sessions.CountAsync(s => s.IsActive && s.Code.StartsWith("D")));
        Assert.Equal(0, await db.Sessions.CountAsync(s => s.IsActive && s.Code.StartsWith("S-D")));

        // Day one opens with the arrival + opening ceremony at 07:00 Riyadh.
        var opening = await db.Sessions.SingleAsync(s => s.Code == "D1-02");
        Assert.Equal(
            "وصول معالي رئيس هيئة الأركان العامة وبدء فعاليات افتتاح الملتقى",
            opening.TitleArabic);
        Assert.Equal(new DateOnly(2026, 11, 23), DateOnly.FromDateTime(opening.Start.Date));

        // The five محاور / axes and their session links (section 3 + 6 of the SQL).
        Assert.Equal(5, await db.Themes.CountAsync(t => t.IsActive && t.Code.StartsWith("AXIS-")));
        Assert.NotEmpty(await db.SessionThemes.ToListAsync());

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

    [Fact]
    public async Task Every_seeded_asset_reference_resolves_to_stored_file_bytes()
    {
        // BUG-001 regression — SIMF_App_SpeakerPhotos.sql seeds StoredFile ROWS whose
        // bytes ship as a deployable folder that only production copied by hand, so
        // every seeded speaker photo 404'd behind the UI placeholder. The seeder now
        // materialises those bytes (and retires any row it cannot back with bytes),
        // so NO seeded asset reference is left pointing at nothing.
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var storage = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.Files.Abstractions.IFileStorageProvider>();

        var seeded = await db.StoredFiles.AsNoTracking()
            .Where(file => file.IsActive
                && file.SourceType == SIMF.Common.Enums.FileSourceType.Upload
                && file.CreatedBy == Guid.Empty
                && file.StorageKey != null)
            .Select(file => new { file.Id, file.Service, file.StorageKey, file.IsEncrypted })
            .ToListAsync();

        // The 23 SIMF-4 speaker headshots the content SQL seeds.
        Assert.NotEmpty(seeded);
        Assert.Contains(seeded, file => file.Service == SIMF.Common.Enums.FileService.SpeakerPhoto);

        foreach (var file in seeded)
        {
            var bytes = await storage.ReadAsync(file.StorageKey!, file.IsEncrypted);
            Assert.True(
                bytes is { Length: > 0 },
                $"seeded {file.Service} file {file.Id} has no bytes at '{file.StorageKey}'");
        }
    }

    [Fact]
    public async Task Seeds_the_registration_baseline_lookups_and_is_idempotent()
    {
        // The profile save REQUIRES interests + an organisation, so a fresh
        // environment must come up with both populated - an empty table blocks
        // registration outright, which is how the first production install ended
        // up having its rows typed in by hand. These moved out of IdentitySeeder
        // into SIMF_App_Lookups.sql on 2026-08-30; the double run proves the
        // guards still hold (the counts must not grow on the second pass).
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var interestCount = await db.Interests.CountAsync();
        var organisationCount = await db.Organisations.CountAsync();

        // The ten baseline interests and the nine baseline organisations, plus
        // the migration-seeded catch-all organisation.
        Assert.Equal(10, interestCount);
        Assert.Equal(10, organisationCount);
        Assert.Contains(db.Interests, interest => interest.Name == "Maritime Security");
        Assert.Contains(db.Organisations, o => o.Name == "Royal Saudi Naval Forces");
        // The catch-all keeps a visitor whose organisation is missing from being
        // blocked. It is seeded by the migration under a FIXED id, so it is
        // asserted by that id rather than by a name anyone can edit.
        Assert.Contains(
            db.Organisations,
            o => o.Id == SIMF.Domain.Organisations.Organisation.OtherId);

        await RunRosterAsync();
        Assert.Equal(interestCount, await db.Interests.CountAsync());
        Assert.Equal(organisationCount, await db.Organisations.CountAsync());
    }

    [Fact]
    public async Task Seeds_the_eight_profile_types_with_their_tier_and_picker_flags()
    {
        // Three separate guarantees ride on these rows, and all three used to be
        // asserted against IdentitySeeder:
        //  * VVIP / VIP exist, are visitor-side (so they flow through the standard
        //    visitor approval queue) and carry IsVipTier - the flag that decides
        //    who may self-reserve a VIP-tier seat and what the app reads as isVip.
        //  * IsAppRegisterable follows MobileAppRole: the CP-only operational
        //    types (Staff, Moderator) ship HIDDEN from the app sign-up picker.
        //  * "Normal" - the single type a self-registering visitor is locked to -
        //    is NEVER hidden, which would silently break mobile self-registration.
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        SIMF.Domain.Profiles.UserProfileType Type(string name) =>
            db.ProfileTypes.Single(profileType => profileType.Name == name);

        Assert.True(Type("VVIP").IsForVisitor, "VVIP must be a visitor-side tier");
        Assert.True(Type("VIP").IsForVisitor, "VIP must be a visitor-side tier");
        Assert.True(Type("VVIP").IsVipTier, "VVIP must be marked as a VIP tier");
        Assert.True(Type("VIP").IsVipTier, "VIP must be marked as a VIP tier");
        Assert.False(Type("Normal").IsVipTier, "Normal is not a VIP tier");

        Assert.False(Type("Staff").IsAppRegisterable, "Staff must be CP-only");
        Assert.False(Type("Moderator").IsAppRegisterable, "Moderator must be CP-only");
        Assert.True(Type("Normal").IsAppRegisterable, "Normal must stay registerable");
        Assert.True(Type("VVIP").IsAppRegisterable);
        Assert.True(Type("VIP").IsAppRegisterable);
        Assert.True(Type("Media").IsAppRegisterable);
        Assert.True(Type("Sponsor").IsAppRegisterable);
        Assert.True(Type("Exhibitor").IsAppRegisterable);

        // Every seeded type carries a badge code, and no two share one. Code 0
        // means "unassigned" and is invisible to the offline badge desk, so a
        // seed that forgot to allocate one would not fail anything until a gate
        // was offline at the venue.
        var codes = await db.ProfileTypes.Where(type => type.IsActive)
            .Select(type => type.Code).ToListAsync();
        Assert.DoesNotContain((short)0, codes);
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public async Task Seeds_every_cms_content_block_the_app_and_the_landing_read()
    {
        // 54 bilingual blocks, and the point of the test is that not one of them
        // can go missing quietly: a dropped row does not fail a build, it just
        // renders an empty section at the venue. The landing keys come from the
        // SHARED constants the Website proxy reads, so the SQL and the proxy
        // cannot drift on a key string - the same guarantee the C# seeder had
        // when it referenced those constants directly.
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        string[] cyberKeys =
        [
            "cyber.title", "cyber.intro",
            "cyber.pillar.01.title", "cyber.pillar.01.body",
            "cyber.pillar.02.title", "cyber.pillar.02.body",
            "cyber.pillar.03.title", "cyber.pillar.03.body",
            "cyber.pillar.04.title", "cyber.pillar.04.body",
            "cyber.pillar.05.title", "cyber.pillar.05.body",
            "cyber.reference",
        ];
        var expected = cyberKeys
            .Concat(SIMF.Common.LandingHeroContentKeys.All)
            .Concat(SIMF.Common.LandingSectionContentKeys.All)
            .Concat(["terms", "about"])
            .ToList();
        Assert.Equal(54, expected.Count);

        var seeded = await db.ContentBlocks.AsNoTracking()
            .Where(block => expected.Contains(block.Key))
            .ToListAsync();

        var missing = expected.Except(seeded.Select(block => block.Key)).ToList();
        Assert.True(
            missing.Count == 0,
            "SIMF_App_ContentBlocks.sql did not seed: " + string.Join(", ", missing));
        Assert.All(seeded, block =>
        {
            Assert.True(block.IsActive, $"{block.Key} must be active");
            Assert.False(string.IsNullOrWhiteSpace(block.Content), $"{block.Key} has no English copy");
            Assert.False(string.IsNullOrWhiteSpace(block.ContentArabic), $"{block.Key} has no Arabic copy");
        });

        // The app splits Terms and About on the newline and renders each line as
        // one card, so a stray carriage return would show up on every card. The
        // SQL builds them with NCHAR(10) concatenation for exactly this reason.
        foreach (var key in new[] { "terms", "about" })
        {
            var block = seeded.Single(candidate => candidate.Key == key);
            Assert.DoesNotContain('\r', block.Content);
            Assert.DoesNotContain('\r', block.ContentArabic);
            Assert.Contains('\n', block.Content);
            Assert.Contains('\n', block.ContentArabic);
        }

        await RunRosterAsync();
        Assert.Equal(
            seeded.Count,
            await db.ContentBlocks.CountAsync(block => expected.Contains(block.Key)));
    }

    [Fact]
    public async Task Seeds_the_default_ai_prompt_catalogue()
    {
        // One prompt per AI feature, all on the offline Echo provider so a fresh
        // install and this suite run with no key configured.
        await RunRosterAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        string[] expected =
        [
            "question-filter", "faq-answer", "assistance", "translate",
            "live-translation", "live-sign-language", "session-summary", "cp-assistant",
        ];
        var seeded = await db.AiPrompts.AsNoTracking()
            .Where(prompt => expected.Contains(prompt.Key))
            .ToListAsync();

        var missing = expected.Except(seeded.Select(prompt => prompt.Key)).ToList();
        Assert.True(
            missing.Count == 0,
            "SIMF_App_AiPrompts.sql did not seed: " + string.Join(", ", missing));
        Assert.All(seeded, prompt =>
        {
            Assert.Equal(SIMF.Common.Enums.AiProvider.Echo, prompt.Provider);
            Assert.Equal(1, prompt.Version);
            Assert.True(prompt.IsActive);
            Assert.False(string.IsNullOrWhiteSpace(prompt.SystemPrompt));
            Assert.False(string.IsNullOrWhiteSpace(prompt.UserPromptTemplate));
        });
        // Every feature is covered exactly once, so a caller resolving a prompt
        // by feature can never find the catalogue silently empty.
        Assert.Equal(
            Enum.GetValues<SIMF.Common.Enums.AiFeature>().Length,
            seeded.Select(prompt => prompt.Feature).Distinct().Count());

        await RunRosterAsync();
        Assert.Equal(
            seeded.Count,
            await db.AiPrompts.CountAsync(prompt => expected.Contains(prompt.Key)));
    }

    private async Task RunRosterAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SqlContentSeeder>()
            .RunAsync(SqlContentSeeder.RosterFiles, CancellationToken.None);
    }
}
