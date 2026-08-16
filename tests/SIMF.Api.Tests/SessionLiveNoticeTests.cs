// FR-702 (owner decision 2026-07-31) — the session's informational live notice.
// SIMF-FDS-007 §5.1 originally specified FR-702 as a geographic restriction ("the
// live stream is available only within the Riyadh region"); the owner reversed it
// verbatim — "No restriction, this is only notification and be added to session".
// So these tests pin the notice as free bilingual text authored per session that is
// SERVED WITH the stream and gates nothing: no region check, no location lookup, and
// the feed stays reachable by an anonymous caller exactly as before.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SessionLiveNoticeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // The notice as an admin would author it in the CP — a calm, informational line.
    private const string NoticeEnglish =
        "This broadcast is provided for the forum audience.";
    private const string NoticeArabic =
        "يُقدَّم هذا البث لجمهور الملتقى.";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionLiveNoticeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_with_a_live_notice_round_trips_on_the_admin_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            NewSessionRequest(hall.Id, notice: NoticeEnglish, noticeArabic: NoticeArabic),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(NoticeEnglish, detail.LiveNotice);
        Assert.Equal(NoticeArabic, detail.LiveNoticeArabic);

        // And the CP edit form must be able to read it back on a fresh GET.
        var read = await GetAuthAsync($"/api/v1/admin/sessions/{detail.Id}", token);
        var stored = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(NoticeEnglish, stored.LiveNotice);
        Assert.Equal(NoticeArabic, stored.LiveNoticeArabic);
    }

    [Fact]
    public async Task Update_clears_the_live_notice_back_to_null()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            NewSessionRequest(hall.Id, notice: NoticeEnglish, noticeArabic: NoticeArabic),
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(NoticeEnglish, created.LiveNotice);

        // The admin empties both inputs — a blank notice stores null, which is
        // simply "no banner"; nothing else about the session changes.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = created.Title,
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                Start = created.Start,
                End = created.End,
                IsActive = true,
                Type = created.Type,
                LiveNotice = "   ",
                LiveNoticeArabic = null,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var edited = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Null(edited.LiveNotice);
        Assert.Null(edited.LiveNoticeArabic);
    }

    [Fact]
    public async Task Update_round_trips_a_live_notice_added_after_creation()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions", NewSessionRequest(hall.Id), token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Null(created.LiveNotice);

        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = created.Title,
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                Start = created.Start,
                End = created.End,
                IsActive = true,
                Type = created.Type,
                LiveNotice = NoticeEnglish,
                LiveNoticeArabic = NoticeArabic,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var edited = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(NoticeEnglish, edited.LiveNotice);
        Assert.Equal(NoticeArabic, edited.LiveNoticeArabic);
    }

    // §7 (validation triple-lock) — 512 is the EF column cap, so an over-length
    // notice must come back as a clean bilingual 400, not a SaveChanges truncation
    // 500. Both languages are asserted (every SIMF error carries EN + AR).
    [Fact]
    public async Task Create_with_an_over_length_live_notice_is_400_SESSION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            NewSessionRequest(hall.Id, notice: new string('n', 513)),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalid, body.Error!.Code);
        Assert.Equal(
            "The live notice must be 512 characters or fewer.", body.Error.Message);
        Assert.Equal(
            "يجب أن يكون إشعار البث المباشر 512 حرفاً أو أقل.", body.Error.MessageArabic);
    }

    [Fact]
    public async Task Update_with_an_over_length_arabic_live_notice_is_400_SESSION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions", NewSessionRequest(hall.Id), token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = created.Title,
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                Start = created.Start,
                End = created.End,
                IsActive = true,
                Type = created.Type,
                LiveNoticeArabic = new string('ن', 513),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);

        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionInvalid, body.Error!.Code);
        Assert.Equal(
            "The Arabic live notice must be 512 characters or fewer.",
            body.Error.Message);
        Assert.Equal(
            "يجب أن يكون الإشعار العربي للبث المباشر 512 حرفاً أو أقل.",
            body.Error.MessageArabic);
    }

    // Exactly 512 characters is the boundary the CP input allows — accepted.
    [Fact]
    public async Task Create_with_a_live_notice_at_the_512_boundary_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();
        var notice = new string('n', 512);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            NewSessionRequest(hall.Id, notice: notice),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(notice, detail.LiveNotice);
    }

    [Fact]
    public async Task Public_detail_exposes_the_live_notice()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            NewSessionRequest(hall.Id, notice: NoticeEnglish, noticeArabic: NoticeArabic),
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        // Anonymous client — no Authorization header, no location, no region hint.
        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        Assert.Equal(NoticeEnglish, detail.LiveNotice);
        Assert.Equal(NoticeArabic, detail.LiveNoticeArabic);
    }

    [Fact]
    public async Task Public_detail_omits_the_live_notice_when_none_is_authored()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions", NewSessionRequest(hall.Id), token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;

        Assert.Null(detail.LiveNotice);
        Assert.Null(detail.LiveNoticeArabic);
    }

    // The owner decision in one assertion: the notice is a NOTIFICATION, not a gate.
    // A session that carries both a live feed and a notice still hands the feed to an
    // anonymous caller — nothing is withheld, and no restriction notice replaces it.
    [Fact]
    public async Task A_live_notice_does_not_withhold_the_live_stream()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync();

        var request = NewSessionRequest(
            hall.Id, notice: NoticeEnglish, noticeArabic: NoticeArabic);
        request.LiveStreamUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        request.LiveSignLanguageUrl = "https://youtu.be/abc123XYZ_-";

        var create = await PostAuthAsync("/api/v1/admin/sessions", request, token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;

        Assert.Equal(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ", detail.LiveStreamUrl);
        Assert.Equal("https://youtu.be/abc123XYZ_-", detail.LiveSignLanguageUrl);
        Assert.Equal(NoticeEnglish, detail.LiveNotice);
    }

    // -- helpers --------------------------------------------------------------

    private static string NewCode() =>
        "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    // An Event needs no speaker, so the shared request stays valid under the
    // required-type (#3) and min-1-speaker (#4) rules.
    private static AdminCreateSessionRequest NewSessionRequest(
        Guid hallId, string? notice = null, string? noticeArabic = null) =>
        new()
        {
            Code = NewCode(),
            Title = "Live broadcast",
            TitleArabic = "البث المباشر",
            Type = SessionType.Event,
            HallId = hallId,
            Start = SimfClock.Now.AddHours(1),
            End = SimfClock.Now.AddHours(2),
            LiveNotice = notice,
            LiveNoticeArabic = noticeArabic,
        };

    private async Task<Hall> SeedHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Main Auditorium",
            NameArabic = "القاعة الرئيسية",
            Capacity = 100,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"live-notice-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Live Notice Admin",
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
