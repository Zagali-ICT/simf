// FR-MOD-001 — a moderator had no way to DISCOVER which sessions they moderate:
// the app offered the Q&A desk on every session and the missing per-session grant
// only surfaced as a 403 after the tap. GET /app/sessions/moderated is the
// discovery list behind that affordance. These tests pin that it returns exactly
// the caller's own grants (never another moderator's), drops soft-deleted
// sessions, orders soonest-first, and is auth-gated.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ModeratedSessionsTests : IClassFixture<SimfApiFactory>
{
    private const string Route = "/api/v1/app/sessions/moderated";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ModeratedSessionsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Returns_only_the_callers_own_grants_soonest_first()
    {
        var moderator = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(
            _client, _factory);
        var moderatorId = UserIdFromToken(moderator.AccessToken);
        var colleague = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(
            _client, _factory);
        var colleagueId = UserIdFromToken(colleague.AccessToken);

        var later = await SeedSessionAsync(
            "Closing Panel", "الجلسة الختامية", DateTimeOffset.UtcNow.AddDays(2));
        var sooner = await SeedSessionAsync(
            "Opening Panel", "الجلسة الافتتاحية", DateTimeOffset.UtcNow.AddDays(1));
        var somebodyElses = await SeedSessionAsync(
            "Not Mine", "ليست لي", DateTimeOffset.UtcNow.AddHours(6));

        await GrantAsync(later.Id, moderatorId);
        await GrantAsync(sooner.Id, moderatorId);
        await GrantAsync(somebodyElses.Id, colleagueId);

        var rows = await ListAsync(moderator.AccessToken);

        Assert.Equal(2, rows.Count);
        // Soonest first — the moderator's next desk is the top row.
        Assert.Equal(sooner.Id, rows[0].SessionId);
        Assert.Equal(later.Id, rows[1].SessionId);
        // Bilingual title + hall projected, so the app needs no second fetch.
        Assert.Equal("Opening Panel", rows[0].Title);
        Assert.Equal("الجلسة الافتتاحية", rows[0].TitleArabic);
        Assert.Equal("Moderated Hall", rows[0].HallName);
        Assert.Equal("قاعة المحاورين", rows[0].HallNameArabic);
        // A colleague's grant is never visible here.
        Assert.DoesNotContain(rows, r => r.SessionId == somebodyElses.Id);
    }

    // The affordance this list drives must not point at a session the audience can
    // no longer see: a soft-deleted session is not a desk anyone can work.
    [Fact]
    public async Task Drops_a_grant_whose_session_was_soft_deleted()
    {
        var moderator = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(
            _client, _factory);
        var moderatorId = UserIdFromToken(moderator.AccessToken);

        var live = await SeedSessionAsync(
            "Still Running", "ما زالت قائمة", DateTimeOffset.UtcNow.AddDays(1));
        var retired = await SeedSessionAsync(
            "Cancelled", "ملغاة", DateTimeOffset.UtcNow.AddDays(1));
        await GrantAsync(live.Id, moderatorId);
        await GrantAsync(retired.Id, moderatorId);
        await DeactivateSessionAsync(retired.Id);

        var rows = await ListAsync(moderator.AccessToken);

        Assert.Equal(live.Id, Assert.Single(rows).SessionId);
    }

    // A moderator with no grants gets an empty list, not an error — the app renders
    // its own "no sessions assigned" state from it.
    [Fact]
    public async Task Returns_an_empty_list_when_the_caller_moderates_nothing()
    {
        var nobody = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(
            _client, _factory);

        Assert.Empty(await ListAsync(nobody.AccessToken));
    }

    [Fact]
    public async Task Requires_an_authenticated_account()
    {
        var anonymous = await _client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    // -- helpers ---------------------------------------------------------------

    private async Task<IReadOnlyList<ModeratedSessionRow>> ListAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ModeratedSessionRow>>>())!.Data!;
    }

    private async Task<Session> SeedSessionAsync(
        string title, string titleArabic, DateTimeOffset start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Moderated Hall", NameArabic = "قاعة المحاورين",
            Capacity = 80, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = title, TitleArabic = titleArabic,
            HallId = hall.Id,
            Start = start, End = start.AddHours(1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task GrantAsync(Guid sessionId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionModerators.Add(new SessionModerator
        {
            SessionId = sessionId,
            UserId = userId,
            AssignedByUserId = userId,
            AssignedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task DeactivateSessionAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await db.Sessions.FirstAsync(s => s.Id == sessionId);
        session.Deactivate();
        await db.SaveChangesAsync();
    }

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(bytes));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}
