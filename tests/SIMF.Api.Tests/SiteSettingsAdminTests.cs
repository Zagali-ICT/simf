using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Configuration;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-464 — the CP "Site Settings" admin endpoints (`GET`/`PUT /admin/site-settings`):
/// upsert the registration message + social links, gated by Configuration.*. Uses
/// keys (Instagram/Snapchat/TikTok) disjoint from <c>SiteSettingsPublicTests</c>
/// (X + message), and asserts from the PUT response, so the cases stay
/// order-independent on the shared test DB. Partial-update semantics: null =
/// leave unchanged, blank = clear.
/// </summary>
public sealed class SiteSettingsAdminTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SiteSettingsAdminTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PUT_upserts_the_links_and_the_response_reflects_them()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            "/api/v1/admin/site-settings",
            new AdminUpdateSiteSettingsRequest
            {
                Instagram = "https://instagram.com/simf_admin",
                Snapchat = "https://snapchat.com/add/simf_admin",
            },
            token);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = (await put.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!.Data!;
        Assert.Equal("https://instagram.com/simf_admin", saved.Social.Instagram);
        Assert.Equal("https://snapchat.com/add/simf_admin", saved.Social.Snapchat);

        // The admin GET prefill reflects it too.
        var get = await GetAuthAsync("/api/v1/admin/site-settings", token);
        var fromGet = (await get.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!.Data!;
        Assert.Equal("https://instagram.com/simf_admin", fromGet.Social.Instagram);
    }

    [Fact]
    public async Task PUT_with_a_blank_value_clears_the_link()
    {
        var token = await CreateAdministratorAndSignInAsync();
        await PutAuthAsync("/api/v1/admin/site-settings",
            new AdminUpdateSiteSettingsRequest { TikTok = "https://tiktok.com/@simf" }, token);

        var cleared = await PutAuthAsync("/api/v1/admin/site-settings",
            new AdminUpdateSiteSettingsRequest { TikTok = "   " }, token);
        var saved = (await cleared.Content
            .ReadFromJsonAsync<ApiResult<SiteSettingsResponse>>())!.Data!;
        Assert.Null(saved.Social.TikTok); // blank → null (inert)
    }

    [Fact]
    public async Task GET_requires_an_admin()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/admin/site-settings", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers (mirror SystemSettingsTests) ---------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sitecfg-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Site Config Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
