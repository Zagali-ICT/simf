using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-461 — the public site-settings read-path (<c>GET /api/v1/app/site-settings</c>):
/// anonymous, returns the in-code defaults until an admin sets the keys, then
/// surfaces the configured overrides. The two cases use disjoint keys so they
/// stay order-independent on the shared test DB.
/// </summary>
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
        // The Arabic message + Facebook are never set by the override test, so
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
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = SiteSettingKeys.SocialX,
                Value = "https://x.com/simf",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = SiteSettingKeys.RegistrationSuccessMessageEn,
                Value = "Welcome aboard the Forum!",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/v1/app/site-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!;
        Assert.Equal("https://x.com/simf", body.Data!.Social.X);
        Assert.Equal("Welcome aboard the Forum!", body.Data.RegistrationSuccessMessageEn);
    }
}
