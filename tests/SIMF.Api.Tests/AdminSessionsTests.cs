// D-165 (gap doc G3) — admin CRUD over Session (programme sessions
// tied to a Hall + M-to-M Speakers + M-to-M Themes). PDF §2.9.
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
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminSessionsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminSessionsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_returns_the_session_with_hall_and_capacity_resolved()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 120);
        var code = NewCode();

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code,
                Title = "Welcome address",
                TitleArabic = "كلمة افتتاحية",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(code, detail.Code);
        Assert.Equal(hall.Id, detail.HallId);
        Assert.Equal(hall.Name, detail.HallName);
        Assert.Equal(120, detail.HallCapacity);
        Assert.Equal(120, detail.EffectiveCapacity);
        Assert.Null(detail.CapacityOverride);
    }

    [Fact]
    public async Task Capacity_override_wins_over_hall_seat_count()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "Cybersecurity panel",
                TitleArabic = "حلقة الأمن السيبراني",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(3),
                EndUtc = DateTimeOffset.UtcNow.AddHours(4),
                CapacityOverride = 200,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(200, detail.CapacityOverride);
        Assert.Equal(200, detail.EffectiveCapacity);
    }

    [Fact]
    public async Task Create_with_unknown_hall_is_400_SESSION_HALL_NOT_FOUND()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "X", TitleArabic = "س",
                HallId = Guid.NewGuid(),
                StartUtc = DateTimeOffset.UtcNow,
                EndUtc = DateTimeOffset.UtcNow.AddHours(1),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHallNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_end_before_start_is_400_SESSION_INVALID_TIME_WINDOW()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);

        var start = DateTimeOffset.UtcNow.AddHours(5);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "Bad window", TitleArabic = "نافذة خاطئة",
                HallId = hall.Id,
                StartUtc = start,
                EndUtc = start.AddMinutes(-30),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalidTimeWindow, body.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_code_is_409_SESSION_CODE_DUPLICATE()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var code = NewCode();
        var first = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code, Title = "A", TitleArabic = "أ",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code, Title = "B", TitleArabic = "ب",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(3),
                EndUtc = DateTimeOffset.UtcNow.AddHours(4),
            },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Speakers_and_themes_persist_and_round_trip_in_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 200);
        var speakers = new[] { await SeedSpeakerAsync(), await SeedSpeakerAsync() };
        var theme = await SeedThemeAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "Joint panel", TitleArabic = "حلقة مشتركة",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    // B9 — D-225: speaker 0 is a plain speaker, speaker 1 is the host.
                    new(speakers[0].Id, speakers[0].Name, speakers[0].NameArabic, 0),
                    new(speakers[1].Id, speakers[1].Name, speakers[1].NameArabic, 1,
                        SessionSpeakerRole.Host),
                },
                ThemeIds = new List<Guid> { theme.Id },
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(2, detail.Speakers.Count);
        Assert.Equal(speakers[0].Id, detail.Speakers[0].SpeakerId);
        Assert.Equal(0, detail.Speakers[0].DisplayOrder);
        // B9 — D-225: the per-session speaker/host role round-trips.
        Assert.Equal(SessionSpeakerRole.Speaker, detail.Speakers[0].Role);
        Assert.Equal(speakers[1].Id, detail.Speakers[1].SpeakerId);
        Assert.Equal(SessionSpeakerRole.Host, detail.Speakers[1].Role);
        Assert.Single(detail.ThemeIds);
        Assert.Equal(theme.Id, detail.ThemeIds[0]);
    }

    [Fact]
    public async Task Deactivate_makes_the_row_inactive_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Dx", TitleArabic = "د",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var first = await DeleteAuthAsync($"/api/v1/admin/sessions/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await DeleteAuthAsync($"/api/v1/admin/sessions/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var read = await GetAuthAsync($"/api/v1/admin/sessions/{detail.Id}", token);
        var after = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.False(after.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "F", TitleArabic = "ف",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // D-349 — a YouTube live URL (and a YouTube sign-language feed) is accepted
    // and round-trips on the detail.
    [Fact]
    public async Task Create_with_a_youtube_live_url_succeeds_and_round_trips()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Live", TitleArabic = "مباشر",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                LiveStreamUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                LiveSignLanguageUrl = "https://youtu.be/abc123XYZ_-",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ", detail.LiveStreamUrl);
        Assert.Equal("https://youtu.be/abc123XYZ_-", detail.LiveSignLanguageUrl);
    }

    // D-349 — a direct HLS stream URL is still accepted (the fallback path).
    [Fact]
    public async Task Create_with_an_hls_live_url_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "HLS", TitleArabic = "بث",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                LiveStreamUrl = "https://live.example.sa/stream.m3u8",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // D-349 — a URL that is neither YouTube nor HLS/MP4 is rejected (400).
    [Fact]
    public async Task Create_with_an_invalid_live_url_is_400_SESSION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Bad", TitleArabic = "خطأ",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                LiveStreamUrl = "https://vimeo.com/12345",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalid, body.Error!.Code);
    }

    // D-349 — a YouTube URL with no extractable video id (a channel/handle/feed
    // link) is rejected, since the player needs the id.
    [Fact]
    public async Task Create_with_a_youtube_url_without_a_video_id_is_400_SESSION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "NoId", TitleArabic = "بدون",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                LiveStreamUrl = "https://www.youtube.com/@SIMFchannel",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalid, body.Error!.Code);
    }

    // D-349 security — a cleartext http live URL is rejected (https only), so a
    // feed cannot be silently downgraded / man-in-the-middled.
    [Fact]
    public async Task Create_with_an_http_live_url_is_400_SESSION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Cleartext", TitleArabic = "غير آمن",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                LiveStreamUrl = "http://live.example.sa/stream.m3u8",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() =>
        "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<Hall> SeedHallAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Main Auditorium",
            NameArabic = "القاعة الرئيسية",
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<Speaker> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Speaker " + Guid.NewGuid().ToString("N")[..4],
            NameArabic = "متحدّث " + Guid.NewGuid().ToString("N")[..4],
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker;
    }

    private async Task<Theme> SeedThemeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var theme = new Theme
        {
            Id = Guid.NewGuid(),
            Code = "T-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Cybersecurity",
            NameArabic = "الأمن السيبراني",
            DisplayOrder = 0,
            PageColor = "#1E3A8A",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();
        return theme;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"session-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Session Admin",
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
