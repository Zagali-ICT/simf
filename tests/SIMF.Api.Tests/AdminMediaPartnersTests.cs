// Tests: SIMF.Api/Endpoints/PublicRelations/MediaPartnerEndpoints.cs
// D-199 (Mockup page 31) — admin CRUD over MediaPartner (list / get / create /
// update / soft-delete + 409 duplicate + 404 + auth gate). Mirrors
// AdminCountriesTests structure (admin created via RoleManager/UserManager +
// sign-in; routes under the global /api/v1 prefix; EnsureDatabaseCreated per class).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Assets;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminMediaPartnersTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminMediaPartnersTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_list_roundtrips_a_media_partner()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                "Roundtrip News", "أخبار الجولة",
                "https://roundtrip.example", 4000),
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!;
        Assert.Equal("Roundtrip News", created.Name);
        Assert.True(created.IsActive);
        Assert.NotEqual(Guid.Empty, created.Id);

        var listResponse = await PostAuthAsync(
            "/api/v1/admin/media-partners/list",
            new GridQuery { Top = 500 },
            token);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = (await listResponse.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMediaPartnerSummary>>>())!.Data!;
        Assert.Contains(page.Items, p => p.Id == created.Id);
    }

    [Fact]
    public async Task List_flips_has_logo_once_a_logo_asset_is_attached()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest($"Logo Wire {suffix}", $"سلك الشعار {suffix}", null, 4700),
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;

        // No logo asset yet → HasLogo false (the grid shows an initials tile).
        var before = await ListAndFindAsync(id, token);
        Assert.NotNull(before);
        Assert.False(before!.HasLogo);

        // Attaching an active MediaPartnerLogo asset flips it true.
        var link = await PutAuthAsync(
            $"/api/v1/admin/assets/MediaPartnerLogo/{id}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://example.com/l.png" },
            token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        var after = await ListAndFindAsync(id, token);
        Assert.NotNull(after);
        Assert.True(after!.HasLogo);
    }

    private async Task<AdminMediaPartnerSummary?> ListAndFindAsync(Guid id, string token)
    {
        var resp = await PostAuthAsync(
            "/api/v1/admin/media-partners/list", new GridQuery { Top = 500 }, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMediaPartnerSummary>>>())!.Data!;
        return page.Items.FirstOrDefault(p => p.Id == id);
    }

    [Fact]
    public async Task Get_returns_the_created_media_partner()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                "Gettable Media", "وسائط قابلة للجلب", null, 4400),
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;

        var getResponse = await GetAuthAsync($"/api/v1/admin/media-partners/{id}", token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var detail = (await getResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!;
        Assert.Equal(id, detail.Id);
        Assert.Equal("وسائط قابلة للجلب", detail.NameArabic);
    }

    [Fact]
    public async Task Create_duplicate_english_name_is_a_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var name = $"Duplicate Wire {Guid.NewGuid():N}";

        var first = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(name, "سلك مكرر", null, 4500),
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(name, "سلك مكرر آخر", null, 4600),
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MediaPartnerNameDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Update_changes_fields_and_persists()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                $"Editable Channel {Guid.NewGuid():N}", "قناة قابلة للتعديل", null, 4700),
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;

        var editedName = $"Edited Channel {Guid.NewGuid():N}";
        var update = await PutAuthAsync(
            $"/api/v1/admin/media-partners/{id}",
            new AdminUpdateMediaPartnerRequest
            {
                Name = editedName,
                NameArabic = "قناة معدّلة",
                Url = "https://edited.example",
                DisplayOrder = 4750,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var getResponse = await GetAuthAsync($"/api/v1/admin/media-partners/{id}", token);
        var detail = (await getResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!;
        Assert.Equal(editedName, detail.Name);
        Assert.Equal("https://edited.example", detail.Url);
        Assert.Equal(4750, detail.DisplayOrder);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await GetAuthAsync(
            $"/api/v1/admin/media-partners/{Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.NotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Create_blank_english_name_is_a_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest("   ", "اسم عربي صالح", null, 4800),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest("Forbidden", "ممنوع", null, 4900),
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"media-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Media Admin",
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
}
