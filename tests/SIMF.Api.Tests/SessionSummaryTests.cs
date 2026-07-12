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
            published: false, sessionActive: true, summaryActive: true, aiModel: "echo");

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
            startUtc: DateTimeOffset.UtcNow.AddDays(1));

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSessionSummaryAsync_AfterStart_ReturnsSummary()
    {
        var sessionId = await SeedSummaryAsync(
            published: true, sessionActive: true, summaryActive: true, aiModel: "echo",
            startUtc: DateTimeOffset.UtcNow.AddHours(-1));

        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{sessionId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal(sessionId, summary.SessionId);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedSummaryAsync(
        bool published, bool sessionActive, bool summaryActive, string? aiModel,
        DateTimeOffset? startUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = NewSession(sessionActive, startUtc);
        db.Halls.Add(NewHallFor(session));
        db.Sessions.Add(session);
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

    private static Session NewSession(bool active, DateTimeOffset? startUtc = null)
    {
        // S-6 — the public summary read gates on the CLOCK (StartUtc <= now), so the
        // published-read tests seed a STARTED session (past start) by default.
        var start = startUtc ?? DateTimeOffset.UtcNow.AddMinutes(-90);
        return new Session
        {
            Id = Guid.NewGuid(),
            Code = "SUM-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Summary Session", TitleArabic = "جلسة الملخص",
            HallId = Guid.Empty, // set by NewHallFor
            StartUtc = start,
            EndUtc = start.AddHours(1),
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
