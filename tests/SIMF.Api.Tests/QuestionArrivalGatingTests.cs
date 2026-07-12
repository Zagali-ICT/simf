// P5.1c — D-242 (FR-704): audience questions are gated by hall arrival. When the
// session's hall has a geofence (D-240), the gate is a HallAttendance arrival
// record (D-241); when it has none, the gate falls back to the D-171 self-assert
// toggle. Covers all three branches.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class QuestionArrivalGatingTests : IClassFixture<SimfApiFactory>
{
    private const double CenterLat = 24.7136;
    private const double CenterLon = 46.6753;
    private const double RadiusMeters = 100;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public QuestionArrivalGatingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Geofenced_session_without_arrival_rejects_the_question_403()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var response = await SubmitAsync(sessionId, visitor, isAtVenue: true); // self-assert ignored
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Geofenced_session_after_arrival_accepts_the_question()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        // Arrive via the geofence first, then ask.
        var arrival = await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = CenterLat, Lon = CenterLon }, visitor);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);

        var response = await SubmitAsync(sessionId, visitor, isAtVenue: false); // gate is the arrival, not the flag
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Arrived_then_departed_can_still_ask_within_the_window()
    {
        // FR-704 gates on "has an enter record this session" (D-242), not current
        // presence — a brief departure does not revoke the right to ask.
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var arrival = await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = CenterLat, Lon = CenterLon }, visitor);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        var departure = await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, departure.StatusCode);

        var response = await SubmitAsync(sessionId, visitor, isAtVenue: false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // S-5 (owner) — a non-geofenced hall has no arrival mechanism, so presence
    // cannot be verified: the client isAtVenue flag is ignored and the question is
    // accepted either way (remote Q&A works). Geofenced halls still require a real
    // arrival (the two tests above).
    [Fact]
    public async Task Non_geofenced_hall_accepts_remote_question_without_a_venue_claim()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);

        var withClaim = await SubmitAsync(sessionId, visitor, isAtVenue: true);
        Assert.Equal(HttpStatusCode.OK, withClaim.StatusCode);

        var withoutClaim = await SubmitAsync(sessionId, visitor, isAtVenue: false);
        Assert.Equal(HttpStatusCode.OK, withoutClaim.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> SubmitAsync(Guid sessionId, string token, bool isAtVenue) =>
        PostAuthAsync($"/api/v1/app/sessions/{sessionId}/questions",
            new SubmitSessionQuestionRequest
            {
                QuestionText = "Is the maritime corridor secure?",
                IsAtVenue = isAtVenue,
            }, token);

    private async Task<Guid> SeedSessionAsync(bool withGeofence)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Gating Hall", NameArabic = "قاعة البوابة",
            Capacity = 100, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            GeofenceCenterLat = withGeofence ? CenterLat : null,
            GeofenceCenterLon = withGeofence ? CenterLon : null,
            GeofenceRadiusMeters = withGeofence ? RadiusMeters : null,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Gating Session", TitleArabic = "جلسة البوابة",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(45),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> SeedApprovedVisitorAsync()
    {
        var email = $"gate-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Gating Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
