// SIMF-FDS-014 (D-261) — shared Contact directory admin CRUD. Mirrors
// AdminBoothsTests / BusinessMeetingsTests (admin sign-in, ApiResult envelope,
// referenced-delete guard, permission gate).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sponsors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.PublicRelations;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ContactsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ContactsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_returns_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var nameAr = "جهة " + Guid.NewGuid().ToString("N")[..6];

        var create = await PostAuthAsync("/api/v1/admin/contacts", new CreateContactRequest
        {
            NameAr = nameAr,
            NameEn = "Acme Naval",
            Email = "info@acme.test",
            PhonePrimary = "+966500000000",
            Website = "https://acme.test",
            FacebookUrl = "https://facebook.com/acme",
        }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
        Assert.Equal(nameAr, created.NameAr);
        Assert.True(created.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/contacts/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var got = (await get.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
        Assert.Equal("Acme Naval", got.NameEn);
        Assert.Equal("info@acme.test", got.Email);
        Assert.Equal("https://facebook.com/acme", got.FacebookUrl);
    }

    [Fact]
    public async Task Create_with_blank_arabic_name_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync("/api/v1/admin/contacts",
            new CreateContactRequest { NameAr = "   ", NameEn = "No name" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_latitude_but_no_longitude_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync("/api/v1/admin/contacts",
            new CreateContactRequest { NameAr = "موقع", Latitude = 24.7, Longitude = null }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_country_projects_country_names_in_list()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var countryId = await FirstActiveCountryIdAsync();
        var nameAr = "بلد " + Guid.NewGuid().ToString("N")[..6];

        var create = await PostAuthAsync("/api/v1/admin/contacts",
            new CreateContactRequest { NameAr = nameAr, CountryId = countryId }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await PostAuthAsync("/api/v1/admin/contacts/list",
            new GridQuery { Search = nameAr, Top = 50 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminContactSummary>>>())!.Data!;
        var row = Assert.Single(page.Items);
        Assert.Equal(countryId, row.CountryId);
        Assert.False(string.IsNullOrWhiteSpace(row.CountryNameEn));
        Assert.False(string.IsNullOrWhiteSpace(row.CountryNameAr));
    }

    [Fact]
    public async Task Update_changes_fields_and_deactivates()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var created = await CreateContactAsync(token, "قديم");

        var update = await PutAuthAsync($"/api/v1/admin/contacts/{created}", new UpdateContactRequest
        {
            NameAr = "جديد",
            NameEn = "Renamed",
            IsActive = false,
        }, token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/contacts/{created}", token);
        var got = (await get.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
        Assert.Equal("جديد", got.NameAr);
        Assert.Equal("Renamed", got.NameEn);
        Assert.False(got.IsActive);
    }

    [Fact]
    public async Task Deactivate_unreferenced_contact_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var created = await CreateContactAsync(token, "للحذف");

        var delete = await DeleteAuthAsync($"/api/v1/admin/contacts/{created}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/contacts/{created}", token);
        var got = (await get.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
        Assert.False(got.IsActive);
    }

    [Fact]
    public async Task Deactivate_contact_referenced_by_active_sponsor_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var created = await CreateContactAsync(token, "مرتبط");
        await SeedSponsorWithContactAsync(created);

        var delete = await DeleteAuthAsync($"/api/v1/admin/contacts/{created}", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactInUse, body.Error!.Code);
    }

    [Fact]
    public async Task Picker_returns_active_matching_contacts()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateContactAsync(token, "بحث " + marker);

        var picker = await GetAuthAsync($"/api/v1/admin/contacts/picker?search={marker}", token);
        Assert.Equal(HttpStatusCode.OK, picker.StatusCode);
        var items = (await picker.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ContactPickerItem>>>())!.Data!;
        Assert.Single(items);
        Assert.Contains(marker, items[0].NameAr);
    }

    [Fact]
    public async Task Update_deactivating_a_referenced_contact_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var created = await CreateContactAsync(token, "مرتبط تحديث");
        await SeedSponsorWithContactAsync(created);

        var update = await PutAuthAsync($"/api/v1/admin/contacts/{created}",
            new UpdateContactRequest { NameAr = "مرتبط تحديث", IsActive = false }, token);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactInUse, body.Error!.Code);
    }

    [Fact]
    public async Task Get_unknown_contact_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync($"/api/v1/admin/contacts/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Update_unknown_contact_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PutAuthAsync($"/api/v1/admin/contacts/{Guid.NewGuid()}",
            new UpdateContactRequest { NameAr = "مجهول" }, token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Delete_unknown_contact_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await DeleteAuthAsync($"/api/v1/admin/contacts/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ContactNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Deactivate_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var created = await CreateContactAsync(token, "تكرار الحذف");

        var first = await DeleteAuthAsync($"/api/v1/admin/contacts/{created}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await DeleteAuthAsync($"/api/v1/admin/contacts/{created}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/contacts/{created}", token);
        var got = (await get.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
        Assert.False(got.IsActive);
    }

    [Fact]
    public async Task Picker_excludes_inactive_contacts()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var created = await CreateContactAsync(token, "مخفي " + marker);
        await DeleteAuthAsync($"/api/v1/admin/contacts/{created}", token);

        var picker = await GetAuthAsync($"/api/v1/admin/contacts/picker?search={marker}", token);
        Assert.Equal(HttpStatusCode.OK, picker.StatusCode);
        var items = (await picker.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ContactPickerItem>>>())!.Data!;
        Assert.Empty(items);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_list()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync("/api/v1/admin/contacts/list",
            new GridQuery { Top = 25 }, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync("/api/v1/admin/contacts",
            new CreateContactRequest { NameAr = "زائر" }, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_picker()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/admin/contacts/picker?search=x", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- SIMF-FDS-014 (D-281) link + public flatten ---------------------------

    [Fact]
    public async Task Sponsor_create_with_contact_link_echoes_in_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var contact = await CreateContactCardAsync(token, "راعٍ مرتبط " + Guid.NewGuid().ToString("N")[..6]);

        var create = await PostAuthAsync("/api/v1/admin/sponsors", new AdminCreateSponsorRequest
        {
            NameEn = "Linked Sponsor " + Guid.NewGuid().ToString("N")[..6],
            NameAr = "راعٍ " + Guid.NewGuid().ToString("N")[..6],
            Tier = (int)SponsorTier.Gold,
            ContactId = contact.Id,
        }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;
        Assert.Equal(contact.Id, created.ContactId);

        var get = await GetAuthAsync($"/api/v1/admin/sponsors/{created.Id}", token);
        var got = (await get.Content.ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;
        Assert.Equal(contact.Id, got.ContactId);
    }

    [Fact]
    public async Task Sponsor_create_with_unknown_contact_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync("/api/v1/admin/sponsors", new AdminCreateSponsorRequest
        {
            NameEn = "Bad Link " + Guid.NewGuid().ToString("N")[..6],
            NameAr = "خطأ " + Guid.NewGuid().ToString("N")[..6],
            Tier = (int)SponsorTier.Bronze,
            ContactId = Guid.NewGuid(),
        }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SponsorInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Public_sponsors_flatten_linked_contact()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var contact = await CreateContactCardAsync(token, "علم البحرية " + Guid.NewGuid().ToString("N")[..6]);
        var sponsorId = await SeedSponsorWithContactAsync(contact.Id);

        var list = await _client.GetAsync("/api/v1/app/sponsors");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var data = (await list.Content.ReadFromJsonAsync<ApiResult<PublicSponsors>>())!.Data!;
        var sponsor = data.Groups.SelectMany(group => group.Sponsors).Single(s => s.Id == sponsorId);

        // The seeded sponsor's own inline name is "راعٍ" — flatten must replace it
        // with the linked Contact's card fields (wire field names unchanged).
        Assert.Equal(contact.NameAr, sponsor.NameAr);
        Assert.Equal(contact.NameEn, sponsor.NameEn);
        Assert.Equal(contact.LogoRelativePath, sponsor.LogoRelativePath);
        Assert.Equal(contact.Website, sponsor.Url);
    }

    [Fact]
    public async Task Public_media_partners_flatten_linked_contact()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var contact = await CreateContactCardAsync(token, "شريك إعلامي " + Guid.NewGuid().ToString("N")[..6]);
        var partnerId = await SeedMediaPartnerWithContactAsync(contact.Id);

        var list = await _client.GetAsync("/api/v1/app/media-partners");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var data = (await list.Content.ReadFromJsonAsync<ApiResult<PublicMediaPartners>>())!.Data!;
        var partner = data.Items.Single(item => item.Id == partnerId);

        Assert.Equal(contact.NameAr, partner.NameArabic);
        Assert.Equal(contact.NameEn, partner.Name);
        Assert.Equal(contact.LogoRelativePath, partner.LogoRelativePath);
        Assert.Equal(contact.Website, partner.Url);
    }

    [Fact]
    public async Task Contact_linked_to_sponsor_and_exhibitor_is_shared()
    {
        // T-01 — one Contact row reused across roles; both referrers carry the
        // same FK (de-duplicated shared directory).
        var token = await CreateAdministratorAndSignInAsync();
        var contact = await CreateContactCardAsync(token, "جهة مشتركة " + Guid.NewGuid().ToString("N")[..6]);

        var sponsor = await PostAuthAsync("/api/v1/admin/sponsors", new AdminCreateSponsorRequest
        {
            NameEn = "Shared S " + Guid.NewGuid().ToString("N")[..6],
            NameAr = "راعٍ " + Guid.NewGuid().ToString("N")[..6],
            Tier = (int)SponsorTier.Silver,
            ContactId = contact.Id,
        }, token);
        Assert.Equal(HttpStatusCode.OK, sponsor.StatusCode);
        var sponsorDetail = (await sponsor.Content.ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!;

        var exhibitor = await PostAuthAsync("/api/v1/admin/exhibitors", new CreateExhibitorRequest
        {
            NameEn = "Shared E " + Guid.NewGuid().ToString("N")[..6],
            NameAr = "عارض " + Guid.NewGuid().ToString("N")[..6],
            ContactId = contact.Id,
        }, token);
        Assert.Equal(HttpStatusCode.OK, exhibitor.StatusCode);
        var exhibitorDetail = (await exhibitor.Content.ReadFromJsonAsync<ApiResult<AdminExhibitorDetail>>())!.Data!;

        Assert.Equal(contact.Id, sponsorDetail.ContactId);
        Assert.Equal(contact.Id, exhibitorDetail.ContactId);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateContactAsync(string token, string nameAr)
    {
        var create = await PostAuthAsync("/api/v1/admin/contacts",
            new CreateContactRequest { NameAr = nameAr }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!.Id;
    }

    private async Task<int> FirstActiveCountryIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .Select(country => country.Id)
            .FirstAsync();
    }

    private async Task<Guid> SeedSponsorWithContactAsync(Guid contactId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var id = Guid.NewGuid();
        appDb.Sponsors.Add(new Sponsor
        {
            Id = id,
            Name = $"Sponsor {Guid.NewGuid():N}",
            NameArabic = "راعٍ",
            Tier = SponsorTier.Bronze,
            ContactId = contactId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedMediaPartnerWithContactAsync(Guid contactId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var id = Guid.NewGuid();
        appDb.MediaPartners.Add(new MediaPartner
        {
            Id = id,
            Name = $"Partner {Guid.NewGuid():N}",
            NameArabic = "شريك",
            LogoRelativePath = "media-partners/inline.png",
            Url = "https://inline.test",
            DisplayOrder = 0,
            ContactId = contactId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return id;
    }

    private async Task<AdminContactDetail> CreateContactCardAsync(string token, string nameAr)
    {
        var create = await PostAuthAsync("/api/v1/admin/contacts", new CreateContactRequest
        {
            NameAr = nameAr,
            NameEn = "Acme " + Guid.NewGuid().ToString("N")[..6],
            LogoRelativePath = "contacts/acme.png",
            Website = "https://acme.test",
        }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResult<AdminContactDetail>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"ct-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "CT Admin",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
