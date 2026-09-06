using System.Globalization;
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
///
/// <para>Also covers the forced-update grace period: <c>minVersion</c> is
/// withheld until <c>minVersionEnforcedFrom</c> arrives, every unreadable or
/// deactivated date fails open, and <c>latestVersion</c> falls back to the
/// minimum so the window is a warning rather than silence. The gate lives on
/// the server precisely so it works on builds that shipped before it existed.
/// </para>
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
        // No case sets iOS minVersion to a value, so it always reads null -
        // absent, seeded empty, or blanked by the fall-back case below - whatever
        // order the class runs in. iOS enforced-from is never set either, so this
        // would read null even if that changed.
        Assert.Null(body.Data!.Ios.MinVersion);
    }

    [Fact]
    public async Task GET_returns_the_admin_configured_values()
    {
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "1.2.0");
        // The minimum only reaches the app once its enforced-from date has
        // arrived; a past date is how an admin asks for immediate enforcement.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(-1));
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
        // (latest + store) so the case stays order-independent on the shared DB.
        // Setting latest explicitly also keeps it independent of the
        // latest-falls-back-to-minimum rule.
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
        // A blank latest falls back to the minimum, so the minimum has to be
        // blank too for "no latest" to mean no latest.
        await UpsertAsync(AppUpdateSettingKeys.IosMinVersion, string.Empty);

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
        // Blanked so the latest-falls-back-to-minimum rule cannot answer for
        // the deactivated key and hide the very thing under test.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, string.Empty);
        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "9.9.9");
        Assert.Equal("9.9.9", (await GetPolicyAsync()).Android.LatestVersion);

        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "9.9.9", isActive: false);

        Assert.Null((await GetPolicyAsync()).Android.LatestVersion);
    }

    [Fact]
    public async Task A_minimum_with_no_enforced_from_date_blocks_nobody()
    {
        // The default, and the point of the whole key: typing a minimum into
        // /admin/configuration used to block the entire fleet on its next
        // launch. Now it does nothing at all until a date is set.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, string.Empty);

        Assert.Null((await GetPolicyAsync()).Android.MinVersion);
    }

    [Fact]
    public async Task A_minimum_is_enforced_from_its_date_and_not_before()
    {
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");

        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(1));
        Assert.Null((await GetPolicyAsync()).Android.MinVersion);

        // Inclusive: the gate is live on the date itself, not the day after.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(0));
        Assert.Equal("9.9.9", (await GetPolicyAsync()).Android.MinVersion);

        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(-30));
        Assert.Equal("9.9.9", (await GetPolicyAsync()).Android.MinVersion);
    }

    [Theory]
    [InlineData("15/10/2026")]   // day-first: a culture-sensitive parse would take it
    [InlineData("10/15/2026")]   // month-first: so would the other culture
    [InlineData("2026-10-15T00:00:00")]
    [InlineData("2026/10/15")]
    [InlineData("tomorrow")]
    public async Task An_enforced_from_date_in_any_other_format_fails_open(string value)
    {
        // Every one of these must be ignored rather than guessed at. A
        // culture-sensitive parse would read 06/10/2026 as June or October
        // depending on where the process runs, and guessing wrong on THIS key
        // blocks the whole fleet months early.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, value);

        Assert.Null((await GetPolicyAsync()).Android.MinVersion);
    }

    [Fact]
    public async Task Deactivating_the_enforced_from_key_lifts_the_gate()
    {
        // The CP refuses to save an empty value, so unticking IsActive is the
        // only off switch an operator has. It has to work, because it is what
        // they will reach for when a forced update is blocking real users.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(-1));
        Assert.Equal("9.9.9", (await GetPolicyAsync()).Android.MinVersion);

        await UpsertAsync(
            AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(-1), isActive: false);

        Assert.Null((await GetPolicyAsync()).Android.MinVersion);
    }

    [Fact]
    public async Task A_minimum_alone_still_prompts_during_the_grace_period()
    {
        // Otherwise the grace period is silence followed by a wall. The
        // dismissible prompt is what makes the window useful to the user.
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom, Days(14));
        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, string.Empty);

        var body = await GetPolicyAsync();

        Assert.Null(body.Android.MinVersion);
        Assert.Equal("9.9.9", body.Android.LatestVersion);
    }

    [Fact]
    public async Task An_explicit_latest_version_wins_over_the_minimum()
    {
        await UpsertAsync(AppUpdateSettingKeys.AndroidMinVersion, "9.9.9");
        await UpsertAsync(AppUpdateSettingKeys.AndroidLatestVersion, "9.9.10");

        Assert.Equal("9.9.10", (await GetPolicyAsync()).Android.LatestVersion);
    }

    /// <summary>An enforced-from value relative to the API's own clock, in the
    /// one format it accepts. Taken from the fake provider the server reads, so
    /// the case cannot drift with the wall clock or the runner's timezone.</summary>
    private string Days(int offset) =>
        DateOnly.FromDateTime(_factory.Time.SimfNow())
            .AddDays(offset)
            .ToString(
                AppUpdateSettingKeys.EnforcedFromDateFormat,
                CultureInfo.InvariantCulture);

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
