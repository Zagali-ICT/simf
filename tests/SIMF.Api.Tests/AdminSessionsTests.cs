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
using SIMF.Domain.SeatReservations;
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
    public async Task List_filtered_by_speakerId_returns_only_that_speakers_sessions()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 100);
        var speaker = await SeedSpeakerAsync();

        // One session links the (freshly seeded) speaker; one does not.
        var linked = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Linked", TitleArabic = "مرتبطة",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speaker.Id, speaker.Name, speaker.NameArabic, 0),
                },
            },
            token);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        var linkedId = (await linked.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!.Id;

        var other = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Other", TitleArabic = "أخرى",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(3),
                EndUtc = DateTimeOffset.UtcNow.AddHours(4),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/sessions/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string> { ["speakerId"] = speaker.Id.ToString() },
            },
            token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        // The speaker is fresh, so exactly the one linked session matches.
        Assert.Contains(page.Items, s => s.Id == linkedId);
        Assert.All(page.Items, s => Assert.Equal(linkedId, s.Id));
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
                // P5 — D-439: AI live-caption text round-trips on create too.
                LiveCaptions = "Welcome to the opening session.",
                LiveCaptionsArabic = "مرحباً بكم في الجلسة الافتتاحية.",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ", detail.LiveStreamUrl);
        Assert.Equal("https://youtu.be/abc123XYZ_-", detail.LiveSignLanguageUrl);
        Assert.Equal("Welcome to the opening session.", detail.LiveCaptions);
        Assert.Equal("مرحباً بكم في الجلسة الافتتاحية.", detail.LiveCaptionsArabic);
    }

    // P5 — D-439 (regression): the Update API DTO previously dropped the live
    // feed URLs (and would have dropped the new caption fields), so editing a
    // session silently wiped its live broadcast. This proves all four live
    // fields round-trip through a PUT — the create path always worked; this is
    // the update path that was broken.
    [Fact]
    public async Task Update_round_trips_all_live_fields()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);

        // Create a plain session with no live broadcast.
        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Edit me", TitleArabic = "عدّلني",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Null(created.LiveStreamUrl);
        Assert.Null(created.LiveCaptions);

        // Edit it to add the full live section.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = created.Title,
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                StartUtc = created.StartUtc,
                EndUtc = created.EndUtc,
                IsActive = true,
                LiveStreamUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                LiveSignLanguageUrl = "https://youtu.be/abc123XYZ_-",
                LiveCaptions = "Live caption line.",
                LiveCaptionsArabic = "سطر الترجمة المباشرة.",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var edited = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ", edited.LiveStreamUrl);
        Assert.Equal("https://youtu.be/abc123XYZ_-", edited.LiveSignLanguageUrl);
        Assert.Equal("Live caption line.", edited.LiveCaptions);
        Assert.Equal("سطر الترجمة المباشرة.", edited.LiveCaptionsArabic);
    }

    [Fact]
    public async Task Update_round_trips_the_seat_selection_mode_override()
    {
        // D-485 — a session can override its hall's seat-selection mode, and the
        // override persists on update (it rides the update route DTO).
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Override me", TitleArabic = "تجاوزني",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                SeatSelectionModeOverride = SeatSelectionMode.OpenSeating,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SeatSelectionMode.OpenSeating, created.SeatSelectionModeOverride);

        // Update clears the override → inherit the hall (null).
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = created.Title,
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                StartUtc = created.StartUtc,
                EndUtc = created.EndUtc,
                IsActive = true,
                SeatSelectionModeOverride = null, // inherit the hall
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var edited = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Null(edited.SeatSelectionModeOverride);
    }

    [Fact]
    public async Task Update_round_trips_the_session_type()
    {
        // Regression: the update route DTO (UpdateSessionRequest) previously
        // OMITTED Type, so a PUT silently wiped the session type to null (the same
        // class as the D-439 live-URL drop). The type must now survive an edit.
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 10);

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(), Title = "Typed", TitleArabic = "مصنّف",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
                Type = SessionType.Workshop,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionType.Workshop, created.Type);

        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            new AdminUpdateSessionRequest
            {
                Code = created.Code,
                Title = "Typed (edited)",
                TitleArabic = created.TitleArabic,
                HallId = created.HallId,
                StartUtc = created.StartUtc,
                EndUtc = created.EndUtc,
                IsActive = true,
                Type = SessionType.Workshop, // the CP form re-sends the type
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var edited = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionType.Workshop, edited.Type);
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

    // -- S-1: booking guards on deactivate / update ---------------------------

    [Fact]
    public async Task DeactivateAsync_WithActiveVisitorBooking_ReturnsConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Pending);

        var delete = await DeleteAuthAsync($"/api/v1/admin/sessions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHasActiveBookings, body.Error!.Code);

        // The session stays active — nothing was orphaned.
        var read = await GetAuthAsync($"/api/v1/admin/sessions/{created.Id}", token);
        var after = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.True(after.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_WithOnlyAdminRowBlock_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        // An admin row-block has no attendee (ReservedForUserId null) — it does
        // not block deletion.
        await SeedReservationAsync(created.Id, reservedForUserId: null, "B", 1, BookingStatus.Approved);

        var delete = await DeleteAuthAsync($"/api/v1/admin/sessions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_HallChange_ReleasesHeldSeats_AndDispatchesNotification()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallA = await SeedHallAsync(capacity: 50);
        var hallB = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hallA.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        var reservationId = await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Approved);

        // Move the session to another hall — every held seat is cascade-released.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, hallId: hallB.Id),
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = await appDb.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == reservationId);
        Assert.NotNull(reservation.ReleasedAt);
        Assert.Equal(BookingStatus.Cancelled, reservation.Status);

        // The affected visitor was notified (BookingRejected kind, D-157 Identity DB).
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.True(await idDb.Notifications.AnyAsync(n =>
            n.UserId == visitorId && n.Kind == NotificationKind.BookingRejected));
    }

    [Fact]
    public async Task UpdateAsync_TimeChange_ReleasesHeldSeats()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        var reservationId = await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Approved);

        // Same hall, but a new time window — the held seat is released.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created,
                startUtc: created.StartUtc.AddHours(2),
                endUtc: created.EndUtc.AddHours(2)),
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = await appDb.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == reservationId);
        Assert.NotNull(reservation.ReleasedAt);
        Assert.Equal(BookingStatus.Cancelled, reservation.Status);
    }

    [Fact]
    public async Task UpdateAsync_NoHallOrTimeChange_KeepsSeats()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        var reservationId = await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Approved);

        // A title-only edit keeps the same hall + window — the seat is untouched.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, title: "Edited title"),
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = await appDb.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == reservationId);
        Assert.Null(reservation.ReleasedAt);
        Assert.NotEqual(BookingStatus.Cancelled, reservation.Status);

        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.False(await idDb.Notifications.AnyAsync(n =>
            n.UserId == visitorId && n.Kind == NotificationKind.BookingRejected));
    }

    [Fact]
    public async Task UpdateAsync_CapacityOverrideBelowHeldSeats_ReturnsConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Approved);
        await SeedReservationAsync(created.Id, reservedForUserId: null, "A", 2, BookingStatus.Approved);

        // Same hall + window, but a capacity override below the 2 held seats.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, capacityOverride: 1),
            token);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCapacityBelowBookings, body.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_DeactivateViaUpdate_WithActiveBooking_ReturnsConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Pending);

        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, isActive: false),
            token);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHasActiveBookings, body.Error!.Code);
    }

    // Verify-correction (major): a single edit that moves the hall AND lowers the
    // capacity override below the old held count must SUCCEED — the seats are
    // cascade-released for that same move, so there is nothing to under-provision.
    [Fact]
    public async Task UpdateAsync_HallChange_WithLowerCapacityOverride_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallA = await SeedHallAsync(capacity: 50);
        var hallB = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hallA.Id);
        var visitorId = await SeedApprovedVisitorUserAsync();
        var reservationId = await SeedReservationAsync(created.Id, visitorId, "A", 1, BookingStatus.Approved);
        await SeedReservationAsync(created.Id, reservedForUserId: null, "A", 2, BookingStatus.Approved);

        // Move to hall B AND set the override below the 2 held seats — allowed.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, hallId: hallB.Id, capacityOverride: 1),
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = await appDb.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == reservationId);
        Assert.NotNull(reservation.ReleasedAt);
    }

    // -- S-2: same-hall time-overlap guard ------------------------------------

    [Fact]
    public async Task CreateAsync_OverlappingHallTime_ReturnsConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);

        var first = await CreateAtAsync(token, hall.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // 30 min into the first session — overlaps.
        var second = await CreateAtAsync(token, hall.Id, start.AddMinutes(30), start.AddMinutes(90));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHallTimeOverlap, body.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_SameHallBackToBack_NonOverlapping_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);

        var first = await CreateAtAsync(token, hall.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Starts exactly when the first ends — half-open, so no overlap.
        var second = await CreateAtAsync(token, hall.Id, start.AddHours(1), start.AddHours(2));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_OverlapDifferentHall_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallA = await SeedHallAsync(capacity: 50);
        var hallB = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);

        var first = await CreateAtAsync(token, hallA.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same time window but a different hall — allowed.
        var second = await CreateAtAsync(token, hallB.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_MoveIntoOccupiedHallTime_ReturnsConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallA = await SeedHallAsync(capacity: 50);
        var hallB = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);

        var occupant = await CreateAtAsync(token, hallA.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, occupant.StatusCode);
        var mover = (await (await CreateAtAsync(token, hallB.Id, start, start.AddHours(1))).Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        // Move the second session into hall A at the same, occupied time → 409.
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{mover.Id}",
            UpdateFrom(mover, hallId: hallA.Id),
            token);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHallTimeOverlap, body.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_SameSessionUnchangedTime_DoesNotSelfConflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var created = await CreateSimpleSessionAsync(token, hall.Id);

        // Re-saving the same session (self is excluded from the overlap scan).
        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{created.Id}",
            UpdateFrom(created, title: "Same slot, new title"),
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_OverlapWithSoftDeletedSession_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);

        var first = (await (await CreateAtAsync(token, hall.Id, start, start.AddHours(1))).Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        // Soft-delete it (no bookings, so deletion is allowed).
        Assert.Equal(HttpStatusCode.OK,
            (await DeleteAuthAsync($"/api/v1/admin/sessions/{first.Id}", token)).StatusCode);

        // A new session in the same hall + time is fine — the inactive row is ignored.
        var second = await CreateAtAsync(token, hall.Id, start, start.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // Verify-correction (minor): a title-only edit of a session that already has a
    // PRE-EXISTING overlapping active sibling (legacy data) must stay saveable —
    // the overlap check runs only when the hall/time actually moves.
    [Fact]
    public async Task UpdateAsync_TitleOnlyEdit_WithPreexistingOverlap_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var start = DateTimeOffset.UtcNow.AddHours(24);
        // Seed two overlapping active sessions directly (bypassing the create guard
        // to simulate legacy data that predates S-2).
        var legacy = await SeedOverlappingPairAsync(hall.Id, start);

        var update = await PutAuthAsync(
            $"/api/v1/admin/sessions/{legacy.Id}",
            new AdminUpdateSessionRequest
            {
                Code = legacy.Code,
                Title = "Legacy edited title",
                TitleArabic = legacy.TitleArabic,
                HallId = legacy.HallId,
                StartUtc = legacy.StartUtc,
                EndUtc = legacy.EndUtc,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
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

    private Task<HttpResponseMessage> CreateAtAsync(
        string token, Guid hallId, DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "Session", TitleArabic = "جلسة",
                HallId = hallId,
                StartUtc = startUtc,
                EndUtc = endUtc,
            },
            token);

    private async Task<AdminSessionDetail> CreateSimpleSessionAsync(string token, Guid hallId)
    {
        var response = await CreateAtAsync(token, hallId,
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private static AdminUpdateSessionRequest UpdateFrom(
        AdminSessionDetail created,
        Guid? hallId = null,
        string? title = null,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int? capacityOverride = null,
        bool? isActive = null) =>
        new()
        {
            Code = created.Code,
            Title = title ?? created.Title,
            TitleArabic = created.TitleArabic,
            HallId = hallId ?? created.HallId,
            StartUtc = startUtc ?? created.StartUtc,
            EndUtc = endUtc ?? created.EndUtc,
            CapacityOverride = capacityOverride ?? created.CapacityOverride,
            IsActive = isActive ?? created.IsActive,
        };

    private async Task<Guid> SeedApprovedVisitorUserAsync()
    {
        var email = $"session-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Session Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<Guid> SeedReservationAsync(
        Guid sessionId, Guid? reservedForUserId, string row, int seat, BookingStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = reservedForUserId is null
                ? SeatReservationKind.AdminReservedRow
                : SeatReservationKind.UserBooking,
            ReservedForUserId = reservedForUserId,
            CreatedByUserId = reservedForUserId ?? Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = status,
        };
        db.SeatReservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<Session> SeedOverlappingPairAsync(Guid hallId, DateTimeOffset start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var first = new Session
        {
            Id = Guid.NewGuid(),
            Code = NewCode(),
            Title = "Legacy A", TitleArabic = "قديمة أ",
            HallId = hallId,
            StartUtc = start, EndUtc = start.AddHours(1),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        var second = new Session
        {
            Id = Guid.NewGuid(),
            Code = NewCode(),
            Title = "Legacy B", TitleArabic = "قديمة ب",
            HallId = hallId,
            StartUtc = start.AddMinutes(30), EndUtc = start.AddMinutes(90),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.AddRange(first, second);
        await db.SaveChangesAsync();
        return first;
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
