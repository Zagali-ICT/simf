using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Seeding;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Tests for the default public-content seed (D-681): the hall + 20-22 Nov
/// programme days + sessions (incl. the opening session), the Highlights news
/// item and the organisation X link all land, and a second run is a no-op
/// (idempotent, keyed on natural keys). The production fixture skips this seeder,
/// so the test invokes it explicitly against its own isolated database.
/// </summary>
public sealed class DefaultContentSeederTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public DefaultContentSeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Seeds_the_default_content_and_is_idempotent()
    {
        await SeedAsync();
        await SeedAsync(); // second run must not duplicate anything

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // The one main hall.
        Assert.Equal(1, await db.Halls.CountAsync(h => h.Code == "MAIN"));

        // The three programme days (20-22 Nov 2026).
        var days = await db.ProgrammeDays
            .Where(d => d.Date >= new DateOnly(2026, 11, 20) && d.Date <= new DateOnly(2026, 11, 22))
            .Select(d => d.Date)
            .ToListAsync();
        Assert.Equal(3, days.Count);
        Assert.Contains(new DateOnly(2026, 11, 20), days);
        Assert.Contains(new DateOnly(2026, 11, 22), days);

        // The opening session carries the placeholder live URL.
        var opening = await db.Sessions.SingleAsync(s => s.Code == "S-D1-01");
        Assert.Equal("الجلسة الافتتاحية", opening.TitleArabic);
        Assert.False(string.IsNullOrWhiteSpace(opening.LiveStreamUrl));
        Assert.Equal(5, await db.Sessions.CountAsync(s => s.Code.StartsWith("S-D")));

        // The Highlights news item.
        Assert.Equal(1, await db.News.CountAsync(n => n.Category == "Highlights"));

        // The organisation X link — owned by IdentitySeeder (the single owner of
        // the org profile), which the factory runs before this seed (D-708).
        var org = await db.OrganizationProfile.SingleAsync(p => p.Id == OrganizationProfile.SingletonId);
        Assert.Equal("https://x.com/SIMF_RSNF", org.XUrl);

        // D-736 — the six app-update policy keys land once (values empty = off).
        var updateKeys = await db.SystemSettings
            .Where(s => s.Key.StartsWith("appUpdate."))
            .ToListAsync();
        Assert.Equal(6, updateKeys.Count);
    }

    [Fact]
    public async Task Reseeding_never_overwrites_an_edited_app_update_value()
    {
        await SeedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var setting = await db.SystemSettings
                .SingleAsync(s => s.Key == SIMF.Common.AppUpdateSettingKeys.IosMinVersion);
            setting.Value = "1.0.0";
            await db.SaveChangesAsync();
        }

        await SeedAsync(); // the re-run must leave the admin edit alone

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var edited = await verifyDb.SystemSettings
            .SingleAsync(s => s.Key == SIMF.Common.AppUpdateSettingKeys.IosMinVersion);
        Assert.Equal("1.0.0", edited.Value);
        Assert.Equal(6, await verifyDb.SystemSettings.CountAsync(s => s.Key.StartsWith("appUpdate.")));
    }

    [Fact]
    public async Task Reseeding_never_resurrects_a_deactivated_app_update_key()
    {
        await SeedAsync();

        // An admin soft-deletes one of the app-update keys.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var setting = await db.SystemSettings
                .SingleAsync(s => s.Key == SIMF.Common.AppUpdateSettingKeys.IosStoreUrl);
            setting.IsActive = false;
            await db.SaveChangesAsync();
        }

        await SeedAsync(); // the re-run keys existence on Key alone, IgnoreActive

        // No duplicate row was inserted (existence is by Key, not by active-Key),
        // and the deactivated key stays deactivated.
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await verifyDb.SystemSettings
            .Where(s => s.Key == SIMF.Common.AppUpdateSettingKeys.IosStoreUrl)
            .ToListAsync();
        Assert.Single(rows);
        Assert.False(rows[0].IsActive);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DefaultContentSeeder>()
            .SeedAsync(CancellationToken.None);
    }
}
