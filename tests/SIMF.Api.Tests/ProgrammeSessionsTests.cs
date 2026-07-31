// D-199 (gap doc G3, Mockup pages 16-17) — public Programme/Sessions reads.
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
using SIMF.Contracts.Programme;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
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
        var start = SimfClock.Now.AddDays(2).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            new[] { themeId }, start, start.AddHours(1));

        // Anonymous client — no Authorization header.
        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
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
    public async Task List_flags_sessions_with_a_published_summary()
    {
        // A8 (D-237) — HasPublishedSummary is true only for a session whose محضر is
        // ACTIVE, team-APPROVED, and carries a PublishedAt stamp (owner 2026-07-19);
        // false for no summary, a draft (PublishedAt == null), or a soft-deleted
        // (IsActive == false) summary.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var day = SimfClock.Now.AddDays(12).Date;

        var withPublished = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(9), day.AddHours(10));
        var withDraft = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(11), day.AddHours(12));
        var withInactive = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(13), day.AddHours(14));
        var withNone = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(15), day.AddHours(16));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            SessionSummary Summary(Guid sessionId, bool published, bool active) => new()
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                KeyPoints = "kp", KeyPointsArabic = "kp",
                Recommendations = "rec", RecommendationsArabic = "rec",
                Speakers = "spk", SpeakersArabic = "spk",
                FullText = "full", FullTextArabic = "full",
                IsActive = active,
                // Owner 2026-07-19 — HasPublishedSummary now requires team approval too,
                // so a published summary also carries the review + approval stamps (the
                // D-611 constraint requires ReviewSubmittedAt whenever ApprovedAt is set).
                ReviewSubmittedAt = published ? SimfClock.Now : null,
                ApprovedAt = published ? SimfClock.Now : null,
                PublishedAt = published ? SimfClock.Now : null,
                CreatedAt = SimfClock.Now,
            };
            db.SessionSummaries.AddRange(
                Summary(withPublished.Id, published: true, active: true),
                Summary(withDraft.Id, published: false, active: true),
                Summary(withInactive.Id, published: true, active: false));
            await db.SaveChangesAsync();
        }

        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
        var items = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!.Items;

        Assert.True(items.Single(i => i.Id == withPublished.Id).HasPublishedSummary);
        Assert.False(items.Single(i => i.Id == withDraft.Id).HasPublishedSummary);
        Assert.False(items.Single(i => i.Id == withInactive.Id).HasPublishedSummary);
        Assert.False(items.Single(i => i.Id == withNone.Id).HasPublishedSummary);
    }

    [Fact]
    public async Task Public_list_item_carries_the_body_and_speaker_cards()
    {
        // D-252 (Mockup screen 16/17): the cached agenda payload also drives the
        // session detail/preview, so each list row now carries the body + the
        // ordered speaker cards (previously detail-only).
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var themeId = await CreateThemeAsync(admin);
        var start = SimfClock.Now.AddDays(6).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            new[] { themeId }, start, start.AddHours(1));

        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        var item = Assert.Single(body.Items, i => i.Id == created.Id);

        Assert.Equal("Welcome address.", item.Description);
        Assert.Equal("كلمة ترحيبية.", item.DescriptionArabic);

        Assert.NotNull(item.Speakers);
        var speaker = Assert.Single(item.Speakers!);
        Assert.Equal("Dr. Amal Badawi", speaker.Name);
        Assert.Equal("Chief Scientist", speaker.Title);
    }

    [Fact]
    public async Task Session_speaker_carries_country_flag_and_photo()
    {
        // §7 (Mockup screen 17 "المتحدثون"): the speaker shown with a session
        // carries the country (the client renders the flag from CountryId) + the
        // photo — on both the cached list and the detail.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var start = SimfClock.Now.AddDays(3).Date.AddHours(9);
        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            if (!await db.Countries.AnyAsync(c => c.Id == 682))
            {
                db.Countries.Add(new Country
                {
                    Id = 682, Code = "SA",
                    Name = "Saudi Arabia", NameArabic = "السعودية", DisplayOrder = 0,
                });
            }
            var speaker = await db.Speakers.SingleAsync(s => s.Id == speakerId);
            speaker.CountryId = 682;
            speaker.PhotoRelativePath = "speakers/amal.webp";
            await db.SaveChangesAsync();
        }

        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
        var listItem = Assert.Single(
            (await list.Content.ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!.Items,
            i => i.Id == created.Id);
        var listSpeaker = Assert.Single(listItem.Speakers!);
        Assert.Equal(682, listSpeaker.CountryId);
        Assert.False(string.IsNullOrEmpty(listSpeaker.CountryNameEn));
        Assert.False(string.IsNullOrEmpty(listSpeaker.CountryNameAr));
        Assert.Equal("speakers/amal.webp", listSpeaker.PhotoRelativePath);

        var detail = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        var detailSpeaker = Assert.Single(
            (await detail.Content.ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!.Speakers);
        Assert.Equal(682, detailSpeaker.CountryId);
        Assert.Equal("speakers/amal.webp", detailSpeaker.PhotoRelativePath);
    }

    [Fact]
    public async Task Session_detail_carries_live_stream_urls_when_set()
    {
        // §8 (Mockup screen 25/26): a session with a manually-set live URL is
        // surfaced to the app (LIVE player); the sign-language feed too.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var start = SimfClock.Now.AddDays(2).Date.AddHours(9);
        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var session = await db.Sessions.SingleAsync(s => s.Id == created.Id);
            session.LiveStreamUrl = "https://live.example/stream.m3u8";
            session.LiveSignLanguageUrl = "https://live.example/sign.m3u8";
            // P5 — D-439: the AI live-caption text is surfaced on the same wire.
            session.LiveCaptions = "Caption text appears here.";
            session.LiveCaptionsArabic = "يظهر نص الترجمة هنا.";
            await db.SaveChangesAsync();
        }

        var detail = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        var body = (await detail.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        Assert.Equal("https://live.example/stream.m3u8", body.LiveStreamUrl);
        Assert.Equal("https://live.example/sign.m3u8", body.LiveSignLanguageUrl);
        Assert.Equal("Caption text appears here.", body.LiveCaptions);
        Assert.Equal("يظهر نص الترجمة هنا.", body.LiveCaptionsArabic);
    }

    [Fact]
    public async Task Detail_with_multiple_themes_and_speakers_returns_each_once()
    {
        // A6 — the detail read projects themes + speakers as independent per-
        // collection sub-selects (was a multi-Include of two SIBLING collections,
        // which JOINed into one rowset). Seeding a session with 2 themes AND 3
        // speakers proves the collections are NOT cross-multiplied: a cartesian
        // would return 2×3 = 6 rows of each with duplicated ids.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speaker1 = await CreateSpeakerAsync(admin);
        var speaker2 = await CreateSpeakerAsync(admin);
        var speaker3 = await CreateSpeakerAsync(admin);
        var theme1 = await CreateThemeAsync(admin);
        var theme2 = await CreateThemeAsync(admin);
        var start = SimfClock.Now.AddDays(7).Date.AddHours(9);

        var create = await PostAuthAsync("/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = $"S{Guid.NewGuid():N}".Substring(0, 8),
                Title = "Panel Session",
                TitleArabic = "جلسة حوارية",
                Description = "A multi-speaker panel.",
                DescriptionArabic = "جلسة بعدة متحدثين.",
                HallId = hallId,
                Type = SessionType.Session,
                Start = start,
                End = start.AddHours(1),
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speaker1, "", "", 0),
                    new(speaker2, "", "", 1),
                    new(speaker3, "", "", 2),
                },
                ThemeIds = new List<Guid> { theme1, theme2 },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var detail = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = (await detail.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;

        // Each collection appears exactly once per row — no cartesian blow-up.
        Assert.Equal(3, body.Speakers.Count);
        Assert.Equal(3, body.Speakers.Select(s => s.Id).Distinct().Count());
        // Speakers stay ordered by their SessionSpeaker.DisplayOrder (0,1,2).
        Assert.Equal(
            new[] { speaker1, speaker2, speaker3 },
            body.Speakers.Select(s => s.Id).ToArray());
        Assert.Equal(2, body.Themes.Count);
        Assert.Equal(2, body.Themes.Select(t => t.Id).Distinct().Count());
        Assert.Contains(theme1, body.Themes.Select(t => t.Id));
        Assert.Contains(theme2, body.Themes.Select(t => t.Id));
    }

    [Fact]
    public async Task Public_list_is_ordered_by_start_time()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var day = SimfClock.Now.AddDays(10).Date;

        var later = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(14), day.AddHours(15));
        var earlier = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(9), day.AddHours(10));

        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;

        var earlierIndex = IndexOf(body, earlier.Id);
        var laterIndex = IndexOf(body, later.Id);
        Assert.True(earlierIndex >= 0 && laterIndex >= 0);
        Assert.True(earlierIndex < laterIndex,
            "Sessions must be ordered ascending by start time.");
    }

    [Fact]
    public async Task Day_filter_restricts_to_that_event_local_calendar_day()
    {
        // A6c — the ?day= window is the event-local (+03:00) calendar day. These
        // 09:00-UTC sessions (12:00 KSA) sit squarely inside their day under either
        // offset, so this pins the basic same-day-in / next-day-out behaviour; the
        // boundary case is covered by Day_filter_uses_the_event_local_day_boundary.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);

        var dayOne = SimfClock.Now.AddDays(20).Date;
        var dayTwo = dayOne.AddDays(1);

        var onDayOne = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayOne.AddHours(9), dayOne.AddHours(10));
        var onDayTwo = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayTwo.AddHours(9), dayTwo.AddHours(10));

        var filter = dayOne.ToString("yyyy-MM-dd");
        var list = await _client.GetAsync($"/api/v1/app/programme/sessions?day={filter}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(body.Items, i => i.Id == onDayOne.Id);
        Assert.DoesNotContain(body.Items, i => i.Id == onDayTwo.Id);
    }

    [Fact]
    public async Task Day_filter_uses_the_event_local_day_boundary()
    {
        // A6c — a session at 01:00 KSA (= 22:00 UTC the PREVIOUS calendar date)
        // belongs to its KSA day, matching ProgrammeDay.Date and the day-grouped
        // agenda (ListDaysAsync). ?day={KSA date} must INCLUDE it, and ?day={the
        // prior UTC date} must EXCLUDE it. Under the old UTC-midnight window this
        // session filed under the prior UTC day, so this pins the +03:00 boundary.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);

        var ksaDay = SimfClock.Now.AddDays(30).Date;
        var start = ksaDay.AddHours(1);

        // The stored value IS the KSA calendar day — there is no second date to
        // straddle any more. This assertion used to read NotEqual, because 01:00
        // KSA was stored as 22:00 UTC on the PRIOR date; that is exactly the
        // misfiling the conversion removed, so pinning the equality is what keeps
        // it removed.
        Assert.Equal(ksaDay.Date, start.Date);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1));

        var ksaDateStr = ksaDay.ToString("yyyy-MM-dd");
        var priorDateStr = ksaDay.AddDays(-1).ToString("yyyy-MM-dd");

        var underKsaDay = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions?day={ksaDateStr}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(underKsaDay.Items, i => i.Id == created.Id);

        // The real regression risk: a 01:00 session leaking onto the previous day.
        var underPriorDay = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions?day={priorDateStr}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.DoesNotContain(underPriorDay.Items, i => i.Id == created.Id);
    }

    [Fact]
    public async Task Malformed_day_filter_is_rejected_with_400()
    {
        var list = await _client.GetAsync("/api/v1/app/programme/sessions?day=not-a-date");
        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
    }

    [Fact]
    public async Task Category_filter_returns_only_that_category()
    {
        // OA-D6 — ?categoryId= narrows the anonymous agenda to one SessionCategory
        // track server-side. Before the fix the endpoint declared only ?day=, so the
        // unrecognised query key was ignored and BOTH sessions came back.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var day = SimfClock.Now.AddDays(40).Date;

        var inCategory = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(9), day.AddHours(10));
        var otherCategory = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(11), day.AddHours(12));

        var (categoryId, otherCategoryId) =
            await AssignCategoriesAsync(inCategory.Id, otherCategory.Id);

        var body = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions?categoryId={categoryId}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(body.Items, i => i.Id == inCategory.Id);
        Assert.DoesNotContain(body.Items, i => i.Id == otherCategory.Id);

        // The other category keys its own result (and its own output-cache entry).
        var otherBody = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions?categoryId={otherCategoryId}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(otherBody.Items, i => i.Id == otherCategory.Id);
        Assert.DoesNotContain(otherBody.Items, i => i.Id == inCategory.Id);
    }

    [Fact]
    public async Task Category_filter_combines_with_the_day_filter()
    {
        // OA-D6 — the two filters AND together: a session in the right category but
        // on another day is excluded.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var dayOne = SimfClock.Now.AddDays(50).Date;
        var dayTwo = dayOne.AddDays(1);

        var onDayOne = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayOne.AddHours(9), dayOne.AddHours(10));
        var onDayTwo = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), dayTwo.AddHours(9), dayTwo.AddHours(10));

        // Both sessions share ONE category, so only the day filter can separate them.
        Guid categoryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var category = new SessionCategory
            {
                Id = Guid.NewGuid(),
                Name = "Main",
                NameArabic = "رئيسية",
                DisplayOrder = 1,
            };
            db.SessionCategories.Add(category);
            foreach (var id in new[] { onDayOne.Id, onDayTwo.Id })
            {
                var session = await db.Sessions.SingleAsync(s => s.Id == id);
                session.CategoryId = category.Id;
            }
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        var filter = dayOne.ToString("yyyy-MM-dd");
        var body = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions?day={filter}&categoryId={categoryId}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Contains(body.Items, i => i.Id == onDayOne.Id);
        Assert.DoesNotContain(body.Items, i => i.Id == onDayTwo.Id);
    }

    [Fact]
    public async Task Unknown_category_filter_returns_an_empty_list_not_404()
    {
        // OA-D6 — the anonymous agenda must not become a category-id oracle: an id
        // that does not exist matches nothing and still answers 200.
        var list = await _client.GetAsync(
            $"/api/v1/app/programme/sessions?categoryId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.Empty(body.Items);
    }

    /// <summary>OA-D6 — seeds two <c>SessionCategory</c> rows and points one session
    /// at each. The admin session API carries no category field, so the assignment is
    /// made directly on the App DB (the pattern the summary tests already use).</summary>
    private async Task<(Guid First, Guid Second)> AssignCategoriesAsync(
        Guid firstSessionId, Guid secondSessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var first = new SessionCategory
        {
            Id = Guid.NewGuid(),
            Name = "Keynote track",
            NameArabic = "مسار الكلمات",
            DisplayOrder = 1,
        };
        var second = new SessionCategory
        {
            Id = Guid.NewGuid(),
            Name = "Workshop track",
            NameArabic = "مسار الورش",
            DisplayOrder = 2,
        };
        db.SessionCategories.AddRange(first, second);

        var firstSession = await db.Sessions.SingleAsync(s => s.Id == firstSessionId);
        firstSession.CategoryId = first.Id;
        var secondSession = await db.Sessions.SingleAsync(s => s.Id == secondSessionId);
        secondSession.CategoryId = second.Id;

        await db.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    [Fact]
    public async Task Detail_returns_speakers_themes_and_seat_summary()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin, capacity: 80);
        var speakerId = await CreateSpeakerAsync(admin);
        var themeId = await CreateThemeAsync(admin);
        var start = SimfClock.Now.AddDays(3).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            new[] { themeId }, start, start.AddMinutes(45));

        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
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
    public async Task Detail_carries_the_day_ordinal()
    {
        // D-567 — the gold index badge shows the session's 1-based position
        // within its day (ordered by start time). Two sessions on a unique day:
        // the earlier resolves to ordinal 1, the later to ordinal 2.
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin);
        var speakerId = await CreateSpeakerAsync(admin);
        var day = SimfClock.Now.AddDays(40).Date;

        var first = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(9), day.AddHours(10));
        var second = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), day.AddHours(11), day.AddHours(12));

        var firstDetail = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions/{first.Id}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        var secondDetail = (await (await _client.GetAsync(
                $"/api/v1/app/programme/sessions/{second.Id}")).Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;

        Assert.Equal(1, firstDetail.DisplayOrder);
        Assert.Equal(2, secondDetail.DisplayOrder);
    }

    [Fact]
    public async Task Capacity_override_drives_the_seat_summary_capacity()
    {
        var admin = await CreateAdminAsync();
        var hallId = await CreateHallAsync(admin, capacity: 200);
        var speakerId = await CreateSpeakerAsync(admin);
        var start = SimfClock.Now.AddDays(4).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1), capacityOverride: 30);

        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
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
        var start = SimfClock.Now.AddDays(5).Date.AddHours(9);

        var created = await CreateSessionAsync(admin, hallId, speakerId,
            Array.Empty<Guid>(), start, start.AddHours(1));

        await DeleteAuthAsync($"/api/v1/admin/sessions/{created.Id}", admin);

        var list = await _client.GetAsync("/api/v1/app/programme/sessions");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicSessions>>())!.Data!;
        Assert.DoesNotContain(body.Items, i => i.Id == created.Id);

        var get = await _client.GetAsync($"/api/v1/app/programme/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Unknown_session_id_returns_404()
    {
        var get = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{Guid.NewGuid()}");
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
        DateTime start, DateTime end, int? capacityOverride = null)
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
                Start = start,
                Type = SessionType.Session,
                End = end,
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
