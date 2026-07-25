// P4.1a — D-237 (Completion Programme §6.4.1, Mockup screen 34): the public read
// of a session's AI summary / محضر. Covers the published-only gate, the
// both-languages projection, the GeneratedByAi flag (AiModel present/absent),
// and the soft-deleted-session and missing-summary 404s. The read is anonymous
// (published editorial content), so no sign-in is needed here.
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Programme;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionSummaryTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionSummaryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Published_summary_is_returned_with_both_languages_and_the_ai_flag()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: "echo");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal(sessionId, summary.SessionId);
        Assert.Equal("KP-en", summary.KeyPoints);
        Assert.Equal("KP-ar", summary.KeyPointsArabic);
        Assert.Equal("REC-en", summary.Recommendations);
        Assert.Equal("REC-ar", summary.RecommendationsArabic);
        Assert.Equal("SPK-en", summary.Speakers);
        Assert.Equal("SPK-ar", summary.SpeakersArabic);
        Assert.Equal("FULL-en", summary.FullText);
        Assert.Equal("FULL-ar", summary.FullTextArabic);
        Assert.True(summary.GeneratedByAi);
    }

    [Fact]
    public async Task Hand_written_published_summary_has_GeneratedByAi_false()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: null);

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.False(summary.GeneratedByAi);
    }

    [Fact]
    public async Task Unpublished_draft_summary_is_404()
    {
        var sessionId = await SeedSummaryAsync(
            published: false, approved: false, sessionActive: true, summaryActive: true,
            aiModel: "echo");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Owner 2026-07-19 — the public read also requires team APPROVAL, so a summary
    // published by the OLD code (PublishedAt set, ApprovedAt null) is NOT served to the
    // app; this guards legacy data the write-side gate cannot retroactively fix.
    [Fact]
    public async Task Published_but_unapproved_summary_is_404()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, approved: false, sessionActive: true, summaryActive: true,
            aiModel: "echo");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Session_without_a_summary_is_404()
    {
        var sessionId = await SeedSessionOnlyAsync(active: true);

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_session_hides_its_published_summary_404()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: false, summaryActive: true, aiModel: "echo");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // S-6 (owner) — the public read hides a published summary while the session
    // has NOT started yet (a future session), even though PublishedAt is stamped.
    [Fact]
    public async Task GetSessionSummaryAsync_BeforeSessionStarts_ReturnsNull()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: "echo",
            start: DateTimeOffset.UtcNow.AddDays(1));

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSessionSummaryAsync_AfterStart_ReturnsSummary()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: "echo",
            start: DateTimeOffset.UtcNow.AddHours(-1));

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal(sessionId, summary.SessionId);
    }

    // Item #35 (2026-07-20) — the summary surface carries TWO videos: the
    // session's FULL live recording (Session.LiveStreamUrl) and the team's
    // OPTIONAL short summary video (SessionSummary.SummaryVideoUrl). Both are
    // projected onto the public read so the app can render the two labeled players.
    [Fact]
    public async Task Published_summary_surfaces_the_recording_and_summary_video_urls()
    {
        var sessionId = await SeedSummaryWithVideosAsync(
            liveStreamUrl: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            summaryVideoUrl: "https://youtu.be/abcdefghijk");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", summary.RecordingUrl);
        Assert.Equal("https://youtu.be/abcdefghijk", summary.SummaryVideoUrl);
    }

    [Fact]
    public async Task Published_summary_with_no_videos_reports_null_urls()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: "echo");

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Null(summary.RecordingUrl);
        Assert.Null(summary.SummaryVideoUrl);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedSummaryWithVideosAsync(
        string liveStreamUrl, string summaryVideoUrl)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = NewSession(active: true);
        session.LiveStreamUrl = liveStreamUrl;
        db.Halls.Add(NewHallFor(session));
        db.Sessions.Add(session);
        var now = DateTimeOffset.UtcNow;
        db.SessionSummaries.Add(new SessionSummary
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            KeyPoints = "KP-en", KeyPointsArabic = "KP-ar",
            Recommendations = "REC-en", RecommendationsArabic = "REC-ar",
            Speakers = "SPK-en", SpeakersArabic = "SPK-ar",
            FullText = "FULL-en", FullTextArabic = "FULL-ar",
            AiModel = "echo",
            SummaryVideoUrl = summaryVideoUrl,
            IsActive = true,
            ReviewSubmittedAt = now,
            ApprovedAt = now,
            PublishedAt = now,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Guid> SeedSummaryAsync(
        bool published, bool sessionActive, bool summaryActive, string? aiModel,
        DateTimeOffset? start = null, bool approved = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = NewSession(sessionActive, start);
        db.Halls.Add(NewHallFor(session));
        db.Sessions.Add(session);
        // Owner 2026-07-19 — the public read requires team approval, and the D-611
        // check constraint requires ReviewSubmittedAt whenever ApprovedAt is set, so an
        // approved summary carries both stamps. A "published but unapproved" legacy row
        // (approved: false, published: true) is the shape the read guard must hide.
        var reviewedAt = approved ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        db.SessionSummaries.Add(new SessionSummary
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            KeyPoints = "KP-en", KeyPointsArabic = "KP-ar",
            Recommendations = "REC-en", RecommendationsArabic = "REC-ar",
            Speakers = "SPK-en", SpeakersArabic = "SPK-ar",
            FullText = "FULL-en", FullTextArabic = "FULL-ar",
            AiModel = aiModel,
            IsActive = summaryActive,
            ReviewSubmittedAt = reviewedAt,
            ApprovedAt = reviewedAt,
            PublishedAt = published ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Guid> SeedSessionOnlyAsync(bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = NewSession(active);
        db.Halls.Add(NewHallFor(session));
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static Session NewSession(bool active, DateTimeOffset? start = null)
    {
        // S-6 — the public summary read gates on the CLOCK (Start <= now), so the
        // published-read tests seed a STARTED session (past start) by default.
        var startValue = start ?? DateTimeOffset.UtcNow.AddMinutes(-90);
        return new Session
        {
            Id = Guid.NewGuid(),
            Code = "SUM-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Summary Session", TitleArabic = "جلسة الملخص",
            HallId = Guid.Empty, // set by NewHallFor
            Start = startValue,
            End = startValue.AddHours(1),
            IsActive = active, CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static Hall NewHallFor(Session session)
    {
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Summary Hall", NameArabic = "قاعة الملخص",
            Capacity = 100, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        session.HallId = hall.Id;
        return hall;
    }
}
