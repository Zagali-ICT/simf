using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Organization;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-495 — the Organization / About profile endpoints. The public read
/// (<c>GET /app/organization-profile</c>) is anonymous, returns the seeded edition
/// config, and supports the D-173 <c>Last-Modified</c> / <c>304</c> revalidation. The
/// admin GET/PUT are gated by OrganizationProfile.View / .Manage and validate URLs.
/// </summary>
public sealed class OrganizationProfileTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public OrganizationProfileTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_public_is_anonymous_and_returns_the_seeded_edition()
    {
        var response = await _client.GetAsync("/api/v1/app/organization-profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.LastModified);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<OrganizationProfileResponse>>())!;
        Assert.True(body.Success);
        Assert.Equal("The International Maritime Forum", body.Data!.Name);
        Assert.Equal(2026, body.Data.CurrentYear);
        Assert.Equal("Open", body.Data.Status);
        Assert.NotEmpty(body.Data.AboutItems);
        Assert.NotEmpty(body.Data.Details);
    }

    [Fact]
    public async Task GET_public_returns_304_when_not_modified()
    {
        var first = await _client.GetAsync("/api/v1/app/organization-profile");
        var lastModified = first.Content.Headers.LastModified!.Value;

        var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/app/organization-profile");
        request.Headers.IfModifiedSince = lastModified;
        var second = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task GET_admin_requires_an_admin()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync(
            "/api/v1/admin/organization-profile", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PUT_admin_saves_the_profile_and_reflects_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            "/api/v1/admin/organization-profile",
            new AdminUpdateOrganizationProfileRequest
            {
                Name = "The International Maritime Forum",
                NameArabic = "الملتقى الدولي البحري",
                Title = "The Saudi International Maritime Forum",
                TitleArabic = "الملتقى البحري السعودي الدولي",
                CurrentYear = 2026,
                Status = "Open",
                LiveStreamUrl = "https://youtube.com/watch?v=simf2026",
                BackgroundVideoUrl = "https://youtu.be/rmW5sJTp-Zo",
                Facebook = "https://facebook.com/simf",
                AboutItems =
                [
                    new AdminOrganizationAboutItem
                    {
                        Title = "Mission", TitleArabic = "الرسالة",
                        Text = "Advance maritime dialogue", TextArabic = "تعزيز الحوار البحري",
                        DisplayOrder = 0,
                    },
                ],
                Details =
                [
                    // A bilingual value (organiser name) — round-trips ValueArabic.
                    new AdminOrganizationDetail
                    {
                        Name = "Organiser", NameArabic = "الجهة المنظمة",
                        Value = "Royal Saudi Naval Forces",
                        ValueArabic = "القوات البحرية الملكية السعودية",
                        DisplayOrder = 0,
                    },
                    // A language-neutral value (a year) — ValueArabic left blank →
                    // surfaces as null so the app falls back to Value.
                    new AdminOrganizationDetail
                    {
                        Name = "Year", NameArabic = "السنة", Value = "2026", DisplayOrder = 1,
                    },
                ],
            },
            token);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = (await put.Content
            .ReadFromJsonAsync<ApiResult<OrganizationProfileResponse>>())!.Data!;
        Assert.Equal("https://youtube.com/watch?v=simf2026", saved.LiveStreamUrl);
        Assert.Equal("https://youtu.be/rmW5sJTp-Zo", saved.BackgroundVideoUrl);
        Assert.Equal("https://facebook.com/simf", saved.Social.Facebook);
        Assert.Single(saved.AboutItems);
        Assert.Equal(2, saved.Details.Count);
        var organiser = saved.Details.Single(d => d.Name == "Organiser");
        Assert.Equal("القوات البحرية الملكية السعودية", organiser.ValueArabic);
        var year = saved.Details.Single(d => d.Name == "Year");
        Assert.Null(year.ValueArabic);
    }

    [Fact]
    public async Task PUT_admin_rejects_a_non_http_url()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            "/api/v1/admin/organization-profile",
            new AdminUpdateOrganizationProfileRequest
            {
                Name = "n", NameArabic = "ن", Title = "t", TitleArabic = "ت",
                CurrentYear = 2026, Status = "Open",
                LiveStreamUrl = "javascript:alert(1)",
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    // -- Helpers (mirror SiteSettingsAdminTests) ------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"orgprofile-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Org Profile Admin",
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
