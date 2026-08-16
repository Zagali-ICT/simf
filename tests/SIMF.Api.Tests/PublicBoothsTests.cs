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
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
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

    // P6 — D-440 / D-766: the app renders the exhibitor's logo from the exhibitor's
    // OWN id (the ExhibitorLogo owner, Exhibitor.Id) — the detail carries ExhibitorId
    // when the booth is linked, null when it has no exhibitor. The retired
    // ExhibitorContactId wire field (the old CompanyLogo owner via the removed shared
    // Contact directory) now always emits null.
    [Fact]
    public async Task Public_booth_carries_the_exhibitor_id_for_the_logo_and_null_contact_id()
    {
        var exhibitorId = Guid.NewGuid();
        var boothWithExhibitor = Guid.NewGuid();
        var boothNoExhibitor = Guid.NewGuid();
        var now = SimfClock.Now;
        var code1 = NewCode();
        var code2 = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "SAMI",
                NameArabic = "سامي",
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothWithExhibitor,
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

        // The retired Contact-directory field is null on every row now.
        Assert.Null(rows.Single(b => b.Id == boothWithExhibitor).ExhibitorContactId);
        Assert.Null(rows.Single(b => b.Id == boothNoExhibitor).ExhibitorContactId);

        // The detail carries the exhibitor's own id (the ExhibitorLogo owner) when
        // linked, and null ExhibitorContactId either way.
        var withExhibitor = (await (await _client.GetAsync($"/api/v1/app/booths/{boothWithExhibitor}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal(exhibitorId, withExhibitor.ExhibitorId);
        Assert.Null(withExhibitor.ExhibitorContactId);

        var noExhibitor = (await (await _client.GetAsync($"/api/v1/app/booths/{boothNoExhibitor}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Null(noExhibitor.ExhibitorId);
        Assert.Null(noExhibitor.ExhibitorContactId);
    }

    // #9 — the public booth wire carries the exhibitor company's country NAME
    // (resolved from the Country lookup on the exhibitor's own inline CountryId),
    // not just the id.
    [Fact]
    public async Task Public_booth_carries_the_resolved_country_name()
    {
        const int countryId = 682; // SA
        var exhibitorId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var now = SimfClock.Now;
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

            // The country now comes from the exhibitor's own inline CountryId
            // (the shared Contact directory was removed).
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "SAMI",
                NameArabic = "سامي",
                CountryId = countryId,
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
    // website + tier and the city, all now inlined directly on the Exhibitor
    // (the shared Contact directory was removed).
    [Fact]
    public async Task Public_booth_detail_carries_the_exhibitor_website_city_and_tier()
    {
        var exhibitorId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var now = SimfClock.Now;
        var code = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "Aramco", NameArabic = "أرامكو",
                City = "Dhahran",
                CityArabic = "الظهران",
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

    // A5 / D-766 — the booth officer name/phone/email are now inline columns on the
    // Booth row (the shared Contact directory was removed), and surface on both the
    // public list and the detail. Covers the nav-based projection's officer fields.
    [Fact]
    public async Task Public_booth_carries_the_inline_officer_fields()
    {
        var boothId = Guid.NewGuid();
        var now = SimfClock.Now;
        var code = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.Set<Booth>().Add(new Booth
            {
                Id = boothId,
                Code = code,
                Name = "Inline", NameArabic = "سطري",
                OfficerName = "ضابط الجناح",
                OfficerPhone = "+966500000000",
                OfficerEmail = "officer@simf.test",
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var rows = (await (await _client.GetAsync("/api/v1/app/booths"))
            .Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;

        var summary = rows.Single(b => b.Id == boothId);
        Assert.Equal("ضابط الجناح", summary.OfficerName);
        Assert.Equal("+966500000000", summary.OfficerPhone);
        Assert.Equal("officer@simf.test", summary.OfficerEmail);

        var detail = (await (await _client.GetAsync($"/api/v1/app/booths/{boothId}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal("ضابط الجناح", detail.OfficerName);
        Assert.Equal("+966500000000", detail.OfficerPhone);
        Assert.Equal("officer@simf.test", detail.OfficerEmail);
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

    // #16 — deactivating an exhibitor must hide its still-active booths from the
    // public read: the soft-deleted exhibitor's live data must not surface, and the
    // booth is excluded from both the list and the detail.
    [Fact]
    public async Task Booth_linked_to_an_inactive_exhibitor_is_absent_from_public_list_and_detail()
    {
        var exhibitorId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var now = SimfClock.Now;
        var code = NewCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            // A soft-deleted (IsActive=false) exhibitor whose booth is still active.
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = "Retired Exhibitor",
                NameArabic = "عارض منسحب",
                IsActive = false,
                CreatedAt = now,
            });
            db.Set<Booth>().Add(new Booth
            {
                Id = boothId,
                Code = code,
                Name = "Orphan Booth",
                NameArabic = "جناح يتيم",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var rows = (await (await _client.GetAsync("/api/v1/app/booths"))
            .Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        Assert.DoesNotContain(rows, b => b.Id == boothId);

        var detail = await _client.GetAsync($"/api/v1/app/booths/{boothId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
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

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
