// D-199 — public anonymous Booth reads (Mockup page 22 + the 2D venue map).
// Mirrors the public Delegations read tests in DelegationsTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.Common;
using SIMF.Domain.Contacts;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PublicBoothsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicBoothsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_succeeds()
    {
        var response = await _client.GetAsync("/api/v1/app/booths");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!;
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
    }

    [Fact]
    public async Task Created_active_booth_appears_in_public_list_and_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = code,
                Name = "Maritime Security Solutions",
                NameArabic = "حلول الأمن البحري",
                Sector = "Defense Systems",
                MapX = 3.0,
                MapY = 4.0,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var list = await _client.GetAsync("/api/v1/app/booths");
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        Assert.Contains(rows, b => b.Id == created.Id && b.Code == code);

        var detailResponse = await _client.GetAsync($"/api/v1/app/booths/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(3.0, detail.MapX);
    }

    // P6 — D-440: the public booth wire carries the exhibitor's Contact id (the
    // CompanyLogo owner) so the app can render the real logo; null when the
    // exhibitor has no linked Contact.
    [Fact]
    public async Task Public_booth_carries_the_exhibitor_contact_id_for_the_logo()
    {
        var contactId = Guid.NewGuid();
        var exhibitorId = Guid.NewGuid();
        var boothWithLogo = Guid.NewGuid();
        var boothNoExhibitor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var code1 = NewCode();
        var code2 = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            // Exhibitor.ContactId is a real FK → the Contact (the CompanyLogo
            // owner) must exist first.
            db.Set<Contact>().Add(new Contact
            {
                Id = contactId,
                NameArabic = "سامي",
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "SAMI",
                NameArabic = "سامي",
                ContactId = contactId,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothWithLogo,
                Code = code1,
                Name = "Booth A",
                NameArabic = "جناح أ",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothNoExhibitor,
                Code = code2,
                Name = "Booth B",
                NameArabic = "جناح ب",
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var list = await _client.GetAsync("/api/v1/app/booths");
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;

        var withLogo = rows.Single(b => b.Id == boothWithLogo);
        Assert.Equal(contactId, withLogo.ExhibitorContactId);

        var noExhibitor = rows.Single(b => b.Id == boothNoExhibitor);
        Assert.Null(noExhibitor.ExhibitorContactId);

        var detail = (await (await _client.GetAsync($"/api/v1/app/booths/{boothWithLogo}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal(contactId, detail.ExhibitorContactId);
    }

    // #9 — the public booth wire carries the exhibitor company's country NAME
    // (resolved from the Country lookup on Contact.CountryId), not just the id.
    [Fact]
    public async Task Public_booth_carries_the_resolved_country_name()
    {
        const int countryId = 682; // SA
        var contactId = Guid.NewGuid();
        var exhibitorId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var code = NewCode();
        string expectedName;
        string expectedNameArabic;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            // Find-or-create the lookup row so the test is robust whether or not
            // the environment pre-seeds the ISO country set.
            var country = await db.Set<Country>().FindAsync(countryId);
            if (country is null)
            {
                country = new Country
                {
                    Id = countryId,
                    Code = "SA",
                    Name = "Saudi Arabia",
                    NameArabic = "السعودية",
                    IsActive = true,
                    CreatedAt = now,
                };
                db.Add(country);
            }
            expectedName = country.Name;
            expectedNameArabic = country.NameArabic;

            db.Set<Contact>().Add(new Contact
            {
                Id = contactId,
                NameArabic = "سامي",
                CountryId = countryId,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "SAMI",
                NameArabic = "سامي",
                ContactId = contactId,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothId,
                Code = code,
                Name = "Booth C",
                NameArabic = "جناح ج",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var list = await _client.GetAsync("/api/v1/app/booths");
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        var booth = rows.Single(b => b.Id == boothId);
        Assert.Equal(countryId, booth.CountryId);
        Assert.Equal(expectedName, booth.CountryName);
        Assert.Equal(expectedNameArabic, booth.CountryNameArabic);

        var detail = (await (await _client.GetAsync($"/api/v1/app/booths/{boothId}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal(expectedName, detail.CountryName);
    }

    // Wave 3 (Figma 1439:11881) — the booth detail surfaces the exhibitor's
    // website + tier and the city resolved from the exhibitor's Contact.
    [Fact]
    public async Task Public_booth_detail_carries_the_exhibitor_website_city_and_tier()
    {
        var contactId = Guid.NewGuid();
        var exhibitorId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var code = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.Set<Contact>().Add(new Contact
            {
                Id = contactId,
                NameArabic = "أرامكو",
                City = "Dhahran",
                CityArabic = "الظهران",
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "Aramco", NameArabic = "أرامكو",
                ContactId = contactId,
                Website = "https://aramco.com",
                Tier = ExhibitorTier.Premium,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothId,
                Code = code,
                Name = "Aramco Booth", NameArabic = "جناح أرامكو",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var detail = (await (await _client.GetAsync($"/api/v1/app/booths/{boothId}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal("https://aramco.com", detail.Website);
        Assert.Equal("Dhahran", detail.City);
        Assert.Equal("الظهران", detail.CityArabic);
        Assert.Equal((int)ExhibitorTier.Premium, detail.Tier);
        Assert.Equal("Premium", detail.TierName);
    }

    [Fact]
    public async Task Public_detail_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/app/booths/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_booth_is_absent_from_public_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, Name = "Hidden", NameArabic = "مخفي" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var detailResponse = await _client.GetAsync($"/api/v1/app/booths/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"booth-pub-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Booth Pub Admin",
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
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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
