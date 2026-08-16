using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-461 / D-495 — the public site-settings read-path (<c>GET /api/v1/app/site-settings</c>):
/// anonymous, returns the in-code defaults until an admin sets the values, then
/// surfaces the configured overrides. The social links + welcome message now live on
/// the singleton <see cref="OrganizationProfile"/> (migrated out of the old SystemSetting
/// keys); each case touches a distinct field so they stay order-independent on the
/// shared test DB.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SiteSettingsPublicTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SiteSettingsPublicTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_is_anonymous_and_returns_the_defaults_for_unset_keys()
    {
        var response = await _client.GetAsync("/api/v1/app/site-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
        Assert.True(body.Success);
        // The Arabic message + Facebook are never set by the override tests, so
        // they always read the defaults regardless of test order.
        Assert.Equal(SiteSettingKeys.DefaultRegistrationMessageAr,
            body.Data!.RegistrationSuccessMessageAr);
        Assert.Null(body.Data.Social.Facebook);
    }

    [Fact]
    public async Task GET_returns_the_admin_configured_overrides()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await db.OrganizationProfile
                .SingleAsync(p => p.Id == OrganizationProfile.SingletonId);
            profile.XUrl = "https://x.com/simf";
            profile.RegistrationSuccessMessage = "Welcome aboard the Forum!";
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/v1/app/site-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
        Assert.Equal("https://x.com/simf", body.Data!.Social.X);
        Assert.Equal("Welcome aboard the Forum!", body.Data.RegistrationSuccessMessageEn);
    }

    [Fact]
    public async Task GET_drops_a_non_http_social_url()
    {
        // D-467 (security) — a non-http(s) social value (e.g. one stored via the
        // generic /admin/configuration page, bypassing the dedicated page's
        // validation) must be dropped to null on read so it can never become an
        // `a.href` / launched link. Uses YouTube (untouched by the other cases).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await db.OrganizationProfile
                .SingleAsync(p => p.Id == OrganizationProfile.SingletonId);
            profile.YouTubeUrl = "javascript:alert(document.cookie)";
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/v1/app/site-settings");
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
        Assert.Null(body.Data!.Social.YouTube);
    }

    [Fact]
    public async Task GET_reflects_the_CP_Session_rating_toggle_in_sessionRatingEnabled()
    {
        // The seeded "Session" rating type is active by default → the app-facing
        // flag reads true; deactivating it in the CP flips the flag to false so the
        // app suppresses the after-watch rate prompt.
        var enabled = (await (await _client.GetAsync("/api/v1/app/site-settings"))
            .Content.ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
        Assert.True(enabled.Data!.SessionRatingEnabled);

        await SetSessionRatingActiveAsync(false);
        try
        {
            var disabled = (await (await _client.GetAsync("/api/v1/app/site-settings"))
                .Content.ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
            Assert.False(disabled.Data!.SessionRatingEnabled);
        }
        finally
        {
            await SetSessionRatingActiveAsync(true);
        }
    }

    private async Task SetSessionRatingActiveAsync(bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await db.RatingTypes.SingleAsync(t => t.Code == "Session");
        type.IsActive = active;
        await db.SaveChangesAsync();
    }
}
