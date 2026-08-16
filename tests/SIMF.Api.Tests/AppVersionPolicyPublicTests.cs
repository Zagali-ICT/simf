using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-736 — the public app-update policy read-path
/// (<c>GET /api/v1/app/version-policy</c>): anonymous, returns null for every
/// unset/blank knob (the app fails open on null), surfaces the admin-configured
/// values, sanitises the store URL to http(s)-only (D-467) and ignores
/// deactivated keys. Each case writes its own keys immediately before the GET —
/// xUnit runs a class serially, so the reads always see the case's own state.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AppVersionPolicyPublicTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AppVersionPolicyPublicTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_is_anonymous_and_returns_null_for_unset_keys()
    {
        var response = await _client.GetAsync("/api/v1/app/version-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AppVersionPolicyResponse>>())!;
        Assert.True(body.Success);
        // iOS minVersion is never set by any other case, so it always reads
        // null (absent row or a seeded empty value) regardless of test order.
        Assert.Null(body.Data!.Ios.MinVersion);
    }

    [Fact]
    public async Task GET_returns_the_admin_configured_values()
    {
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "1.2.0");
        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "1.4.0");
        await UpsertAsync(AppUpdateSettingKeys.AndroidStoreUrl,
            "https://play.google.com/store/apps/details?id=sa.simf.app");

        var body = await GetPolicyAsync();

        Assert.Equal("1.2.0", body.Android.MinVersion);
        Assert.Equal("1.4.0", body.Android.LatestVersion);
        Assert.Equal("https://play.google.com/store/apps/details?id=sa.simf.app",
            body.Android.StoreUrl);
        Assert.Null(body.Ios.MinVersion);
    }

    [Fact]
    public async Task GET_returns_the_ios_configured_values()
    {
        // iOS decodes on its own branch. Sets-then-asserts its own fields
        // (latest + store) so the case stays order-independent on the shared DB;
        // the "unset" test above owns Ios.MinVersion, which nothing here touches.
        await UpsertAsync(AppUpdateSettingKeys.IosLatestVersion, "1.4.0");
        await UpsertAsync(AppUpdateSettingKeys.IosStoreUrl,
            "https://apps.apple.com/app/id123456789");

        var body = await GetPolicyAsync();

        Assert.Equal("1.4.0", body.Ios.LatestVersion);
        Assert.Equal("https://apps.apple.com/app/id123456789", body.Ios.StoreUrl);
    }

    [Fact]
    public async Task GET_returns_null_for_a_blank_value()
    {
        // The seeder pre-creates the keys with empty values — an untouched
        // (or whitespace-edited) row must read as "rule off", not "".
        await UpsertAsync(AppUpdateSettingKeys.IosLatestVersion, "   ");

        var body = await GetPolicyAsync();

        Assert.Null(body.Ios.LatestVersion);
    }

    [Fact]
    public async Task GET_drops_a_non_http_store_url()
    {
        // D-467 (security) — the store URL becomes a launched link on-device,
        // so a non-http(s) value stored via the generic /admin/configuration
        // CRUD must drop to an inert null on read.
        await UpsertAsync(AppUpdateSettingKeys.IosStoreUrl, "javascript:alert(1)");

        var body = await GetPolicyAsync();

        Assert.Null(body.Ios.StoreUrl);
    }

    [Fact]
    public async Task GET_ignores_a_deactivated_key()
    {
        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "9.9.9");
        Assert.Equal("9.9.9", (await GetPolicyAsync()).Android.LatestVersion);

        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "9.9.9", isActive: false);

        Assert.Null((await GetPolicyAsync()).Android.LatestVersion);
    }

    private async Task<AppVersionPolicyResponse> GetPolicyAsync()
    {
        var response = await _client.GetAsync("/api/v1/app/version-policy");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AppVersionPolicyResponse>>())!;
        Assert.True(body.Success);
        return body.Data!;
    }

    private async Task UpsertAsync(string key, string value, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            setting = new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                CreatedAt = SimfClock.Now,
            };
            db.SystemSettings.Add(setting);
        }
        setting.Value = value;
        setting.IsActive = isActive;
        await db.SaveChangesAsync();
    }
}
