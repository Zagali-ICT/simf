// D-199 (gap doc G3, Mockup pages 16-17) — public Programme/Sessions reads.
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

public sealed class ProgrammeSessionsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ProgrammeSessionsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_returns_active_session_with_hall_and_theme()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin, capacity: 120);
        var speakerId = await CreateSpeakerAsync(admin);
        var themeId = await CreateThemeAsync(admin);
        var start = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            new[] { themeId }, start, start.AddHours(1));

        // Anonymous client — no Authorization header.
        var list = await _client.GetAsync("/api/v1/programme/sessions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        var item = Assert.Single(body.Items, i => i.Id == created.Id);
        Assert.Equal("Main Hall", item.HallName);
        Assert.Equal("القاعة الرئيسية", item.HallNameArabic);
        Assert.Equal("Opening Keynote", item.Title);
        Assert.Equal("Keynote", item.PrimaryThemeName);
    }

    [Fact]
    public async Task Public_list_is_ordered_by_start_time()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var day = DateTimeOffset.UtcNow.AddDays(10).Date;

        var later = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(14), day.AddHours(15));
        var earlier = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(9), day.AddHours(10));

        var list = await _client.GetAsync("/api/v1/programme/sessions");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;

        var earlierIndex = IndexOf(body, earlier.Id);
        var laterIndex = IndexOf(body, later.Id);
        Assert.True(earlierIndex >= 0 && laterIndex >= 0);
        Assert.True(earlierIndex < laterIndex,
            "Sessions must be ordered ascending by start time.");
    }

    [Fact]
    public async Task Day_filter_restricts_to_that_utc_calendar_day()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);

        var dayOne = DateTimeOffset.UtcNow.AddDays(20).Date;
        var dayTwo = dayOne.AddDays(1);

        var onDayOne = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayOne.AddHours(9), dayOne.AddHours(10));
        var onDayTwo = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayTwo.AddHours(9), dayTwo.AddHours(10));

        var filter = dayOne.ToString("yyyy-MM-dd");
        var list = await _client.GetAsync($"/api/v1/programme/sessions?day={filter}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(body.Items, i => i.Id == onDayOne.Id);
        Assert.DoesNotContain(body.Items, i => i.Id == onDayTwo.Id);
    }

    [Fact]
    public async Task Malformed_day_filter_is_rejected_with_400()
    {
        var list = await _client.GetAsync("/api/v1/programme/sessions?day=not-a-date");
        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
    }

    [Fact]
    public async Task Detail_returns_speakers_themes_and_seat_summary()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin, capacity: 80);
        var speakerId = await CreateSpeakerAsync(admin);
        var themeId = await CreateThemeAsync(admin);
        var start = DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            new[] { themeId }, start, start.AddMinutes(45));

        var get = await _client.GetAsync($"/api/v1/programme/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        Assert.Equal("Opening Keynote", detail.Title);
        Assert.Equal("Main Hall", detail.HallName);

        var speaker = Assert.Single(detail.Speakers);
        Assert.Equal("Dr. Amal Badawi", speaker.Name);
        Assert.Equal("Chief Scientist", speaker.Title);

        var theme = Assert.Single(detail.Themes);
        Assert.Equal("Keynote", theme.Name);

        // No reservations seeded -> every effective seat is available.
        Assert.Equal(80, detail.Seats.Capacity);
        Assert.Equal(0, detail.Seats.Reserved);
        Assert.Equal(80, detail.Seats.Available);
    }

    [Fact]
    public async Task Capacity_override_drives_the_seat_summary_capacity()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin, capacity: 200);
        var speakerId = await CreateSpeakerAsync(admin);
        var start = DateTimeOffset.UtcNow.AddDays(4).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1), capacityOverride: 30);

        var get = await _client.GetAsync($"/api/v1/programme/sessions/{created.Id}");
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;

        Assert.Equal(30, detail.Seats.Capacity);
        Assert.Equal(30, detail.Seats.Available);
    }

    [Fact]
    public async Task Soft_deleted_session_drops_off_list_and_detail_404s()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var start = DateTimeOffset.UtcNow.AddDays(5).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1));

        await DeleteAuthAsync($"/api/v1/admin/sessions/{created.Id}", admin);

        var list = await _client.GetAsync("/api/v1/programme/sessions");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.DoesNotContain(body.Items, i => i.Id == created.Id);

        var get = await _client.GetAsync($"/api/v1/programme/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Unknown_session_id_returns_404()
    {
        var get = await _client.GetAsync(
            $"/api/v1/programme/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private static int IndexOf(PublicSessions body, Guid id)
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

    private async Task<AdminSessionDetail> CreateSessionAsync(
        string token, Guid hallId, Guid speakerId, IReadOnlyList<Guid> themeIds,
        DateTimeOffset startUtc, DateTimeOffset endUtc, int? capacityOverride = null)
    {
        var create = await PostAuthAsync("/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = $"S{Guid.NewGuid():N}".Substring(0, 8),
                Title = "Opening Keynote",
                TitleArabic = "الكلمة الافتتاحية",
                Description = "Welcome address.",
                DescriptionArabic = "كلمة ترحيبية.",
                HallId = hallId,
                StartUtc = startUtc,
                EndUtc = endUtc,
                CapacityOverride = capacityOverride,
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speakerId, "", "", 0),
                },
                ThemeIds = themeIds.ToList(),
            }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private async Task<Guid> CreateHallAsync(string token, int capacity = 100)
    {
        var create = await PostAuthAsync("/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = $"H{Guid.NewGuid():N}".Substring(0, 8),
                Name = "Main Hall",
                NameArabic = "القاعة الرئيسية",
                Capacity = capacity,
            }, token);
        var body = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        return body.Id;
    }

    private async Task<Guid> CreateSpeakerAsync(string token)
    {
        var create = await PostAuthAsync("/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = $"S{Guid.NewGuid():N}".Substring(0, 8),
                Name = "Dr. Amal Badawi",
                NameArabic = "د. أمل بدوي",
                Rank = "Chief Scientist",
            }, token);
        var body = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;
        return body.Id;
    }

    private async Task<Guid> CreateThemeAsync(string token)
    {
        var create = await PostAuthAsync("/api/v1/admin/themes",
            new AdminCreateThemeRequest
            {
                Code = $"T{Guid.NewGuid():N}".Substring(0, 8),
                Name = "Keynote",
                NameArabic = "كلمة رئيسية",
                DisplayOrder = 0,
                PageColor = "#123456",
            }, token);
        var body = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminThemeSummary>>())!.Data!;
        return body.Id;
    }

    private async Task<string> CreateAdminAsync()
    {
        var email = $"prog-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Prog Admin",
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
