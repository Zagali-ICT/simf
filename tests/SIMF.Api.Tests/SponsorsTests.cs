// D-199 (Mockup page 23) — Sponsors admin CRUD + public grouped list.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sponsors;
using SIMF.Domain.Common;
using SIMF.Domain.Contacts;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-199 — Sponsors module. Mirrors DelegationsTests: public anonymous
/// read (active-only, grouped/ordered by tier) + admin CRUD + 409 duplicate.</summary>
public sealed class SponsorsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SponsorsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_creates_sponsor_and_public_endpoint_returns_it_grouped()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Saudi Aramco",
                NameAr = "أرامكو السعودية",
                Tier = (int)SponsorTier.Platinum,
                Url = "https://aramco.com",
                DisplayOrder = 10,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;
        Assert.Equal((int)SponsorTier.Platinum, created.Tier);

        var publicList = await _client.GetAsync("/api/v1/app/sponsors");
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
        var body = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<PublicSponsors>>())!.Data!;
        Assert.Contains(
            body.Groups.SelectMany(g => g.Sponsors),
            s => s.Id == created.Id);
    }

    [Fact]
    public async Task Public_list_groups_highest_tier_first()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Gold Co", NameAr = "ذهبية",
                Tier = (int)SponsorTier.Gold, DisplayOrder = 0,
            }, admin);
        await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Platinum Co", NameAr = "بلاتينية",
                Tier = (int)SponsorTier.Platinum, DisplayOrder = 0,
            }, admin);

        var list = await _client.GetAsync("/api/v1/app/sponsors");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSponsors>>())!.Data!;

        var platinumIndex = -1;
        var goldIndex = -1;
        for (var i = 0; i < body.Groups.Count; i++)
        {
            if (body.Groups[i].Tier == (int)SponsorTier.Platinum) platinumIndex = i;
            if (body.Groups[i].Tier == (int)SponsorTier.Gold) goldIndex = i;
        }
        Assert.True(platinumIndex >= 0 && goldIndex >= 0);
        Assert.True(platinumIndex < goldIndex,
            "Platinum tier group must precede Gold in the public list.");
    }

    [Fact]
    public async Task Deactivated_sponsor_drops_off_public_list()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Soon Gone", NameAr = "سيختفي قريباً",
                Tier = (int)SponsorTier.Silver, DisplayOrder = 0,
            }, admin);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;

        await DeleteAuthAsync($"/api/v1/admin/sponsors/{created.Id}", admin);

        var list = await _client.GetAsync("/api/v1/app/sponsors");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSponsors>>())!.Data!;
        Assert.DoesNotContain(
            body.Groups.SelectMany(g => g.Sponsors),
            s => s.Id == created.Id);
    }

    [Fact]
    public async Task Duplicate_active_name_in_same_tier_returns_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var first = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Dup Co", NameAr = "شركة مكررة",
                Tier = (int)SponsorTier.Gold, DisplayOrder = 5,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Dup Co 2", NameAr = "شركة مكررة",
                Tier = (int)SponsorTier.Gold, DisplayOrder = 6,
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Public_sponsor_detail_returns_about_tier_and_website()
    {
        // Wave 3 (Figma 1439:11826) — GET /app/sponsors/{id} surfaces the about
        // paragraph + tier + website for the sponsor-detail screen.
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Aramco", NameAr = "أرامكو",
                Tier = (int)SponsorTier.Platinum,
                Url = "https://aramco.com",
                About = "A global energy company.",
                AboutArabic = "شركة طاقة عالمية.",
                DisplayOrder = 0,
            }, admin);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;

        var response = await _client.GetAsync($"/api/v1/app/sponsors/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSponsorDetail>>())!.Data!;
        Assert.Equal("أرامكو", detail.NameAr);
        Assert.Equal((int)SponsorTier.Platinum, detail.Tier);
        Assert.Equal("Platinum", detail.TierName);
        Assert.Equal("A global energy company.", detail.About);
        Assert.Equal("شركة طاقة عالمية.", detail.AboutArabic);
        Assert.Equal("https://aramco.com", detail.Url);
    }

    [Fact]
    public async Task Public_sponsor_detail_for_an_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/app/sponsors/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_sponsor_detail_resolves_city_and_country_from_the_linked_contact()
    {
        // Wave 3 — the city + country on the sponsor detail come from the linked
        // Contact (exercises the Contact-with-country resolve path).
        const int countryId = 682; // SA
        var contactId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        string expectedCountryEn;
        string expectedCountryAr;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var country = await db.Set<Country>().FindAsync(countryId);
            if (country is null)
            {
                country = new Country
                {
                    Id = countryId, Code = "SA",
                    Name = "Saudi Arabia", NameArabic = "السعودية",
                    IsActive = true, CreatedAt = now,
                };
                db.Add(country);
            }
            expectedCountryEn = country.Name;
            expectedCountryAr = country.NameArabic;

            db.Set<Contact>().Add(new Contact
            {
                Id = contactId,
                NameArabic = "أرامكو المرتبطة",
                City = "Dhahran",
                CityArabic = "الظهران",
                CountryId = countryId,
                IsActive = true,
                CreatedAt = now,
            });
            db.Set<Sponsor>().Add(new Sponsor
            {
                Id = sponsorId,
                // Unique name+tier so it does not collide with the other
                // Platinum-"أرامكو" sponsor in this class (the duplicate guard).
                Name = "Aramco Linked", NameArabic = "أرامكو المرتبطة",
                Tier = SponsorTier.Platinum,
                ContactId = contactId,
                About = "A global energy company.",
                IsActive = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var detail = (await (await _client.GetAsync($"/api/v1/app/sponsors/{sponsorId}"))
            .Content.ReadFromJsonAsync<ApiResult<PublicSponsorDetail>>())!.Data!;
        Assert.Equal("Dhahran", detail.City);
        Assert.Equal("الظهران", detail.CityArabic);
        Assert.Equal(countryId, detail.CountryId);
        Assert.Equal(expectedCountryEn, detail.CountryNameEn);
        Assert.Equal(expectedCountryAr, detail.CountryNameAr);
    }

    [Fact]
    public async Task Admin_update_preserves_the_tagline()
    {
        // D-501 regression — editing a sponsor used to wipe its bilingual tagline:
        // the inline UpdateSponsorRequest bind model had no Tagline/TaglineArabic
        // property, so FastEndpoints dropped them and UpdateAsync overwrote the
        // stored value with null. This must round-trip through the HTTP bind layer
        // (a service-level test passes despite the bug — the drop is at binding).
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "Tagline Co", NameAr = "شركة الشعار",
                Tier = (int)SponsorTier.Gold, DisplayOrder = 0,
                Tagline = "Strategic Partner",
                TaglineArabic = "الشريك الاستراتيجي",
            }, admin);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;
        Assert.Equal("Strategic Partner", created.Tagline);

        // Edit changing only the display order, resending the same tagline exactly
        // as the CP form posts it.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sponsors/{created.Id}",
            new AdminUpdateSponsorRequest
            {
                NameEn = "Tagline Co", NameAr = "شركة الشعار",
                Tier = (int)SponsorTier.Gold, DisplayOrder = 5,
                Tagline = "Strategic Partner",
                TaglineArabic = "الشريك الاستراتيجي",
                IsActive = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;
        Assert.Equal("Strategic Partner", updated.Tagline);
        Assert.Equal("الشريك الاستراتيجي", updated.TaglineArabic);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = "x", NameAr = "س",
                Tier = (int)SponsorTier.Bronze, DisplayOrder = 0,
            }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sponsor-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Sponsor Admin",
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
                Email = email, Password = AuthFlow.Password,
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
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
