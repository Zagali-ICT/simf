// D-153 — admin CRUD over Speaker with the enhanced shape (CountryId FK +
// UserProfileId logical FK + bilingual rich text + consent + social URLs).
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
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminSpeakersTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminSpeakersTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_returns_the_speaker_with_country_resolved_when_CountryId_is_supplied()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var response = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = code,
                Name = "Dr. John Doe",
                NameArabic = "د. جون دو",
                Rank = "Captain",
                CountryId = 682, // SA from the seed
                Bio = "Bio en",
                BioArabic = "السيرة الذاتية",
                Qualifications = "MSc, PhD",
                QualificationsArabic = "ماجستير، دكتوراه",
                AllowsMeetingRequests = true,
                AllowsDataSharing = false,
                FacebookUrl = "https://facebook.com/jdoe",
                LinkedInUrl = "https://linkedin.com/in/jdoe",
                XUrl = "https://x.com/jdoe",
                WebsiteUrl = "https://example.com/jdoe",
                DisplayOrder = 10,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        Assert.Equal(code, detail.Code);
        Assert.Equal(682, detail.CountryId);
        Assert.Equal("Saudi Arabia", detail.CountryNameEn);
        Assert.True(detail.AllowsMeetingRequests);
        Assert.False(detail.AllowsDataSharing);
        Assert.Equal("https://x.com/jdoe", detail.XUrl);
        Assert.Equal("https://example.com/jdoe", detail.WebsiteUrl);
    }

    [Fact]
    public async Task List_projects_country_code_and_the_has_photo_flag()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = code, Name = "Flag Test", NameArabic = "علم", CountryId = 682, // SA
            },
            token);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        // No photo yet: the grid row carries the alpha-2 country code and a
        // false HasPhoto so it renders an initials tile, not a broken image.
        var before = await ListAndFindAsync(detail.Id, code, token);
        Assert.NotNull(before);
        Assert.Equal("SA", before!.CountryCode);
        Assert.False(before.HasPhoto);

        // Attaching an active speaker-photo asset (an external link is the
        // cheapest one) flips HasPhoto to true on the next list.
        var link = await PutAuthAsync(
            $"/api/v1/admin/assets/SpeakerPhoto/{detail.Id}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://example.com/p.png" },
            token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        var after = await ListAndFindAsync(detail.Id, code, token);
        Assert.NotNull(after);
        Assert.True(after!.HasPhoto);
    }

    private async Task<AdminSpeakerSummary?> ListAndFindAsync(
        Guid id, string search, string token)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/speakers/list",
            new GridQuery { Top = 200, Search = search },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerSummary>>>())!.Data!;
        return page.Items.FirstOrDefault(s => s.Id == id);
    }

    [Fact]
    public async Task Create_with_an_overlong_WebsiteUrl_is_400_SPEAKER_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = NewCode(),
                Name = "W",
                NameArabic = "و",
                // 257 chars — one past the 256 cap ValidateSocialUrls enforces.
                WebsiteUrl = "https://example.com/" + new string('a', 257),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_unknown_CountryId_is_400_SPEAKER_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = NewCode(),
                Name = "X",
                NameArabic = "س",
                CountryId = 99999, // not in the seed
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_SPEAKER_CODE_DUPLICATE()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var first = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest { Code = code, Name = "A", NameArabic = "أ" },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest { Code = code, Name = "B", NameArabic = "ب" },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Update_persists_the_consent_toggles_and_social_URLs()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var createResponse = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest { Code = code, Name = "Z", NameArabic = "ز" },
            token);
        var detail = (await createResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        Assert.False(detail.AllowsMeetingRequests);
        Assert.False(detail.AllowsDataSharing);

        var update = await PutAuthAsync(
            $"/api/v1/admin/speakers/{detail.Id}",
            new AdminUpdateSpeakerRequest
            {
                Code = detail.Code,
                Name = detail.Name,
                NameArabic = detail.NameArabic,
                AllowsMeetingRequests = true,
                AllowsDataSharing = true,
                LinkedInUrl = "https://linkedin.com/in/z",
                WebsiteUrl = "https://example.com/z",
                DisplayOrder = 0,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        Assert.True(updated.AllowsMeetingRequests);
        Assert.True(updated.AllowsDataSharing);
        Assert.Equal("https://linkedin.com/in/z", updated.LinkedInUrl);
        Assert.Equal("https://example.com/z", updated.WebsiteUrl);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/speakers/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_makes_the_row_inactive_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var createResponse = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest { Code = code, Name = "D", NameArabic = "د" },
            token);
        var detail = (await createResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        var first = await DeleteAuthAsync($"/api/v1/admin/speakers/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await DeleteAuthAsync($"/api/v1/admin/speakers/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // idempotent

        var read = await GetAuthAsync($"/api/v1/admin/speakers/{detail.Id}", token);
        var afterDetail = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        Assert.False(afterDetail.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest { Code = NewCode(), Name = "F", NameArabic = "ف" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "SP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"speaker-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Speaker Admin",
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
