// D-199 — admin CRUD over Booth (Exhibition module, Mockup page 22 + 2D map).
// Mirrors AdminSpeakersTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.Companies;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminBoothsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminBoothsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_returns_the_booth()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var companyId = await SeedCompanyAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = code,
                NameEn = "Advanced Naval Technologies",
                NameAr = "تقنيات بحرية متقدمة",
                CompanyId = companyId,
                OfficerName = "Capt. Khalid",
                OfficerPhone = "+966500000000",
                OfficerEmail = "khalid@booth.test",
                SectorEn = "Defense Systems",
                DescriptionEn = "Cutting-edge naval defense systems.",
                MapX = 12.5,
                MapY = 8.0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;
        Assert.Equal(code, created.Code);
        Assert.Equal(companyId, created.CompanyId);
        Assert.Equal("Capt. Khalid", created.OfficerName);
        Assert.Equal(12.5, created.MapX);
        Assert.True(created.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(companyId, fetched.CompanyId);
        Assert.Equal("Defense Systems", fetched.SectorEn);
    }

    [Fact]
    public async Task Create_with_an_unknown_company_is_a_400_BOOTH_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), NameEn = "X", NameAr = "س",
                CompanyId = Guid.NewGuid(),   // never seeded
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BoothInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_an_inactive_company_is_a_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var dormant = await SeedCompanyAsync(isActive: false);
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), NameEn = "X", NameAr = "س", CompanyId = dormant,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_sponsor_company_is_a_400()
    {
        // Only Exhibitor companies may staff a booth.
        var token = await CreateAdministratorAndSignInAsync();
        var sponsor = await SeedCompanyAsync(type: CompanyType.Sponsor);
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), NameEn = "X", NameAr = "س", CompanyId = sponsor,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_malformed_officer_email_is_a_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), NameEn = "X", NameAr = "س",
                OfficerEmail = "not-an-email",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_booth_sources_its_exhibitor_name_from_the_linked_company()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"Co{Guid.NewGuid():N}"[..8];
        var companyId = await SeedCompanyAsync(nameEn: $"{marker} Industries");
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), NameEn = "Hull 7", NameAr = "بدن 7", CompanyId = companyId,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var publicList = await _client.GetAsync("/api/v1/booths");
        var list = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        var row = Assert.Single(list, b => b.Id == created.Id);
        // The wire field ExhibitorNameEn is now populated from the Company.
        Assert.Equal($"{marker} Industries", row.ExhibitorNameEn);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_BOOTH_CODE_DUPLICATE()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var first = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, NameEn = "A", NameAr = "أ" },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, NameEn = "B", NameAr = "ب" },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BoothCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/booths/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_hides_the_booth_from_the_public_list_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, NameEn = "D", NameAr = "د" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var first = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // idempotent

        // The public anonymous list must no longer contain the deactivated booth.
        var publicList = await _client.GetAsync("/api/v1/booths");
        var list = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        Assert.DoesNotContain(list, b => b.Id == created.Id);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = NewCode(), NameEn = "F", NameAr = "ف" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    // B1 — D-222: seed a company so a booth can reference it. Defaults to an
    // active Exhibitor (the only kind valid for a booth).
    private async Task<Guid> SeedCompanyAsync(
        bool isActive = true,
        CompanyType type = CompanyType.Exhibitor,
        string? nameEn = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            NameEn = nameEn ?? $"Exhibitor {Guid.NewGuid():N}",
            NameAr = "شركة عارضة",
            Type = type,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Companies.Add(company);
        await appDb.SaveChangesAsync();
        return company.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"booth-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Booth Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
