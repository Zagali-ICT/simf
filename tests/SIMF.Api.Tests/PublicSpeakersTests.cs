// D-199 — public anonymous Speaker reads (Mockup pages 19 "Speakers" +
// 20 "Speaker profile"). Mirrors the public Booths read tests in
// PublicBoothsTests; reuses the admin session-create flow from
// ProgrammeSessionsTests to verify the speaker's-sessions projection.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PublicSpeakersTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicSpeakersTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_succeeds()
    {
        var response = await _client.GetAsync("/api/v1/app/speakers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakers>>())!;
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
    }

    [Fact]
    public async Task Created_active_speaker_appears_in_public_list_and_detail()
    {
        var token = await CreateAdminAsync();
        var code = NewCode("S");
        var create = await PostAuthAsync(
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
                AllowsMeetingRequests = true,
                DisplayOrder = 10,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        var list = await _client.GetAsync("/api/v1/app/speakers");
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakers>>())!.Data!;
        var row = Assert.Single(rows.Items, s => s.Id == created.Id);
        Assert.Equal("Dr. John Doe", row.Name);
        Assert.Equal("Captain", row.Rank);
        Assert.Equal("Saudi Arabia", row.CountryNameEn);

        var detailResponse = await _client.GetAsync($"/api/v1/app/speakers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakerDetail>>())!.Data!;
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal("Bio en", detail.Bio);
        Assert.Equal("السيرة الذاتية", detail.BioArabic);
        Assert.True(detail.AllowsMeetingRequests);
        Assert.Empty(detail.Sessions);
    }

    [Fact]
    public async Task Public_list_is_ordered_by_display_order()
    {
        var token = await CreateAdminAsync();
        var later = await CreateSpeakerAsync(token, displayOrder: 90);
        var earlier = await CreateSpeakerAsync(token, displayOrder: 5);

        var list = await _client.GetAsync("/api/v1/app/speakers");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakers>>())!.Data!;

        var earlierIndex = IndexOf(body, earlier);
        var laterIndex = IndexOf(body, later);
        Assert.True(earlierIndex >= 0 && laterIndex >= 0);
        Assert.True(earlierIndex < laterIndex,
            "Speakers must be ordered ascending by DisplayOrder.");
    }

    [Fact]
    public async Task Public_detail_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/app/speakers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_speaker_is_absent_from_list_and_detail_404s()
    {
        var token = await CreateAdminAsync();
        var speakerId = await CreateSpeakerAsync(token, displayOrder: 1);

        var delete = await DeleteAuthAsync($"/api/v1/admin/speakers/{speakerId}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var list = await _client.GetAsync("/api/v1/app/speakers");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakers>>())!.Data!;
        Assert.DoesNotContain(body.Items, s => s.Id == speakerId);

        var detailResponse = await _client.GetAsync($"/api/v1/app/speakers/{speakerId}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Detail_returns_the_speakers_active_sessions()
    {
        var token = await CreateAdminAsync();
        var speakerId = await CreateSpeakerAsync(token, displayOrder: 3);
        var hallId = await CreateHallAsync(token, capacity: 120);
        var start = SimfClock.Now.AddDays(2).Date.AddHours(9);
        var session = await CreateSessionAsync(token, hallId, speakerId, start, start.AddHours(1));

        var detailResponse = await _client.GetAsync($"/api/v1/app/speakers/{speakerId}");
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakerDetail>>())!.Data!;

        var line = Assert.Single(detail.Sessions, s => s.Id == session.Id);
        Assert.Equal("Opening Keynote", line.Title);
        Assert.Equal("Main Hall", line.HallName);
        Assert.Equal("القاعة الرئيسية", line.HallNameArabic);
        Assert.Equal(session.Id, line.Id);
    }

    [Fact]
    public async Task Social_urls_are_hidden_unless_speaker_consents_to_data_sharing()
    {
        var token = await CreateAdminAsync();

        // Speaker A — has NOT consented to data sharing: URLs are stored
        // but must be withheld from the public profile.
        var notSharing = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = NewCode("NS"),
                Name = "Private Pat",
                NameArabic = "بات الخاص",
                AllowsDataSharing = false,
                FacebookUrl = "https://facebook.com/private",
                LinkedInUrl = "https://linkedin.com/in/private",
                XUrl = "https://x.com/private",
                WebsiteUrl = "https://example.com/private",
                DisplayOrder = 50,
            },
            token);
        var notSharingId = (await notSharing.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!.Id;

        var hiddenResponse = await _client.GetAsync($"/api/v1/app/speakers/{notSharingId}");
        var hidden = (await hiddenResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakerDetail>>())!.Data!;
        Assert.Null(hidden.FacebookUrl);
        Assert.Null(hidden.LinkedInUrl);
        Assert.Null(hidden.XUrl);
        Assert.Null(hidden.WebsiteUrl);

        // Speaker B — has consented: the URLs are published.
        var sharing = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = NewCode("SH"),
                Name = "Sharing Sam",
                NameArabic = "سام المشارك",
                AllowsDataSharing = true,
                FacebookUrl = "https://facebook.com/sharing",
                XUrl = "https://x.com/sharing",
                WebsiteUrl = "https://example.com/sharing",
                DisplayOrder = 51,
            },
            token);
        var sharingId = (await sharing.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!.Id;

        var shownResponse = await _client.GetAsync($"/api/v1/app/speakers/{sharingId}");
        var shown = (await shownResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicSpeakerDetail>>())!.Data!;
        Assert.Equal("https://facebook.com/sharing", shown.FacebookUrl);
        Assert.Equal("https://x.com/sharing", shown.XUrl);
        Assert.Equal("https://example.com/sharing", shown.WebsiteUrl);
    }

    // -- Helpers --------------------------------------------------------------

    private static int IndexOf(PublicSpeakers body, Guid id)
    {
        for (var i = 0; i < body.Items.Count; i++)
        {
            if (body.Items[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    private static string NewCode(string prefix) =>
        prefix + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<Guid> CreateSpeakerAsync(string token, int displayOrder)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = NewCode("S"),
                Name = "Dr. Amal Badawi",
                NameArabic = "د. أمل بدوي",
                Rank = "Chief Scientist",
                DisplayOrder = displayOrder,
            },
            token);
        var body = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        return body.Id;
    }

    private async Task<Guid> CreateHallAsync(string token, int capacity)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = $"H{Guid.NewGuid():N}".Substring(0, 8),
                Name = "Main Hall",
                NameArabic = "القاعة الرئيسية",
                Capacity = capacity,
            },
            token);
        var body = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        return body.Id;
    }

    private async Task<AdminSessionDetail> CreateSessionAsync(
        string token, Guid hallId, Guid speakerId,
        DateTime start, DateTime end)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = $"S{Guid.NewGuid():N}".Substring(0, 8),
                Title = "Opening Keynote",
                TitleArabic = "الكلمة الافتتاحية",
                Description = "Welcome address.",
                DescriptionArabic = "كلمة ترحيبية.",
                HallId = hallId,
                Start = start,
                Type = SessionType.Session,
                End = end,
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speakerId, "", "", 0),
                },
                ThemeIds = new List<Guid>(),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private async Task<string> CreateAdminAsync()
    {
        var email = $"speaker-pub-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Speaker Pub Admin",
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
