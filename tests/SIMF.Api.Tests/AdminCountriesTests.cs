// D-151 — admin CRUD over Country (ISO 3166-1 numeric Id manually
// assigned). Seed already contains ~56 priority countries (SA = 682, etc.).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminCountriesTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // ISO numeric ids are hand-assigned, and the fixture database is SHARED and
    // ACCUMULATES across the whole run — so picking one at random from the
    // 900..997 band, 98 slots shared by every country any test creates,
    // eventually collides. When it does, the
    // server's ID-duplicate check fires BEFORE the code / validation rule the
    // test is actually asserting, and the failure reads like a product bug: on
    // 2026-07-29 Duplicate_code_is_a_409_COUNTRY_CODE_DUPLICATE reported
    // COUNTRY_ID_DUPLICATE. Handing ids out from a counter makes each one unique
    // within the run rather than merely probably-unique.
    private static int _nextTestCountryId = 900;

    private static int ClaimCountryId() => Interlocked.Increment(ref _nextTestCountryId);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminCountriesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Seed_includes_Saudi_Arabia_at_id_682()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync("/api/v1/admin/countries/682", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminCountryDetail>>())!.Data!;
        Assert.Equal("SA", detail.Code);
        Assert.Equal("Saudi Arabia", detail.Name);
        Assert.Equal("+966", detail.PhonePrefix);
    }

    [Fact]
    public async Task Seed_includes_UAE_at_id_784()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync("/api/v1/admin/countries/784", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminCountryDetail>>())!.Data!;
        Assert.Equal("AE", detail.Code);
        Assert.Equal("+971", detail.PhonePrefix);
    }

    [Fact]
    public async Task Admin_can_create_a_new_country_with_a_free_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        // Pick an id well outside the seed range to avoid collisions.
        var newId = ClaimCountryId();

        var create = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = newId,
                Code = "ZZ",
                Name = "Test Country",
                NameArabic = "بلد اختبار",
                PhonePrefix = "+999",
                DisplayOrder = 9999,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminCountryDetail>>())!.Data!;
        Assert.Equal(newId, detail.Id);
        Assert.Equal("ZZ", detail.Code);
    }

    [Fact]
    public async Task Duplicate_id_is_a_409_COUNTRY_ID_DUPLICATE()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = 682, // Saudi Arabia — already seeded
                Code = "XA",
                Name = "Duplicate",
                NameArabic = "مكرر",
                PhonePrefix = "+999",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.CountryIdDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_COUNTRY_CODE_DUPLICATE()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = ClaimCountryId(),
                Code = "SA", // already seeded
                Name = "Duplicate Code",
                NameArabic = "رمز مكرر",
                PhonePrefix = "+999",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.CountryCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Code_must_be_two_chars()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = ClaimCountryId(),
                Code = "TOO_LONG", // not 2 chars
                Name = "Bad",
                NameArabic = "سيء",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.CountryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync("/api/v1/admin/countries/999999", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = ClaimCountryId(),
                Code = "ZX",
                Name = "Forbidden",
                NameArabic = "ممنوع",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"country-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Test Country Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
