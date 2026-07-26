// P5.1b — D-241 (FDS-003 §5.4, FR-305/506): attendee-facing hall arrival via the
// GPS geofence. Covers inside/outside the geofence, the no-geofence hall, the
// idempotent re-arrival (one open row), departure, invalid coordinates, the
// pre-arrival status, and the anonymous rejection.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

public sealed class HallAttendanceTests : IClassFixture<SimfApiFactory>
{
    // A fixed venue centre + a tight 100 m geofence.
    private const double CenterLat = 24.7136;
    private const double CenterLon = 46.6753;
    private const double RadiusMeters = 100;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public HallAttendanceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Arrival_inside_the_geofence_opens_an_attendance_row()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var status = await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        Assert.True(status.Arrived);
        Assert.NotNull(status.Enter);
        Assert.Null(status.Leave);
        Assert.Equal(AttendanceMethod.Geofence, status.Method);
    }

    [Fact]
    public async Task Arrival_outside_the_geofence_is_403()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        // ~1.1 km north of the centre — well outside the 100 m radius.
        var response = await PostArriveAsync(sessionId, CenterLat + 0.01, CenterLon, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Arrival_at_a_hall_with_no_geofence_is_400()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);

        var response = await PostArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Re_arriving_is_idempotent_one_open_row()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var first = await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        var second = await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);

        Assert.True(second.Arrived);
        // Same enter time — the second call returned the existing open row.
        Assert.Equal(first.Enter, second.Enter);
    }

    [Fact]
    public async Task Departure_closes_the_open_row()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);
        await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);

        var response = await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = (await response.Content.ReadFromJsonAsync<ApiResult<HallAttendanceStatus>>())!.Data!;
        Assert.False(status.Arrived);
        Assert.NotNull(status.Leave);
    }

    [Fact]
    public async Task Departure_fires_a_session_rating_prompt_once()
    {
        // D-713 (GAP-A) — leaving the hall closes the session attendance and
        // prompts the attendee to rate that session, exactly once even if they
        // re-enter and leave again (shared per-(session, user) dedup).
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        var departed = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, departed.StatusCode);
        Assert.Equal(1, await CountSessionRatingPromptsAsync(sessionId));

        // Re-enter, leave again — the dedup guard means no second prompt.
        await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
        Assert.Equal(1, await CountSessionRatingPromptsAsync(sessionId));
    }

    [Fact]
    public async Task Departure_with_no_open_row_fires_no_rating_prompt()
    {
        // Never arrived → departure is a no-op → no rating prompt.
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await CountSessionRatingPromptsAsync(sessionId));
    }

    [Fact]
    public async Task Departure_fires_no_prompt_when_the_Session_rating_type_is_deactivated()
    {
        // Owner: the session rate prompt must be CP-controllable. With the "Session"
        // rating type deactivated in RatingConfig, leaving the hall still closes the
        // attendance but sends no rating prompt (mirrors the clock-end worker).
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);
        await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        await SetSessionRatingTypeActiveAsync(false);
        try
        {
            var departed = await PostAuthAsync(
                $"/api/v1/app/sessions/{sessionId}/departure", new { }, visitor);
            Assert.Equal(HttpStatusCode.OK, departed.StatusCode);
            Assert.Equal(0, await CountSessionRatingPromptsAsync(sessionId));
        }
        finally
        {
            await SetSessionRatingTypeActiveAsync(true);
        }
    }

    [Fact]
    public async Task Invalid_coordinate_is_400()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var response = await PostArriveAsync(sessionId, 999, 999, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Status_before_arrival_is_not_arrived()
    {
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);

        var response = await GetAuthAsync($"/api/v1/app/sessions/{sessionId}/attendance", visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = (await response.Content.ReadFromJsonAsync<ApiResult<HallAttendanceStatus>>())!.Data!;
        Assert.False(status.Arrived);
    }

    [Fact]
    public async Task Anonymous_arrival_is_401()
    {
        var sessionId = await SeedSessionAsync(withGeofence: true);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = CenterLat, Lon = CenterLon });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Arrival_before_the_session_window_is_409_session_not_live()
    {
        // X-3 — a session starting in an hour is not open for arrivals yet.
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(
            withGeofence: true, startOffsetMin: 60, endOffsetMin: 120);

        var response = await PostArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotLive, body.Error!.Code);
    }

    [Fact]
    public async Task Arrival_when_the_hall_is_at_capacity_is_409()
    {
        // X-2 — a hall of capacity 1 with one attendee already present rejects a
        // second distinct arrival with HALL_AT_CAPACITY.
        var first = await SeedApprovedVisitorAsync();
        var second = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true, capacity: 1);

        var firstArrival = await ArriveAsync(sessionId, CenterLat, CenterLon, first);
        Assert.True(firstArrival.Arrived);

        var response = await PostArriveAsync(sessionId, CenterLat, CenterLon, second);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallAtCapacity, body.Error!.Code);
    }

    [Fact]
    public async Task Re_arrival_of_the_same_attendee_merges_even_when_hall_is_full()
    {
        // X-2 — a re-scan by an attendee who already holds the single open row
        // merges (no capacity denial) because the merge path returns early.
        var visitor = await SeedApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true, capacity: 1);

        var first = await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        var second = await ArriveAsync(sessionId, CenterLat, CenterLon, visitor);
        Assert.Equal(first.Enter, second.Enter);
    }

    [Fact]
    public async Task Concurrent_arrivals_never_exceed_the_hall_capacity()
    {
        // FR-CHK-004 — capacity used to be count-then-decide with no DB constraint,
        // so several arrivals racing a cap-2 hall could all read "room left" and all
        // commit, overfilling it. The count and the insert now share one Serializable
        // transaction (via the execution strategy), so the outcome is exact: precisely
        // `cap` arrivals succeed and every loser is a clean 409 HALL_AT_CAPACITY.
        const int cap = 2;
        var sessionId = await SeedSessionAsync(withGeofence: true, capacity: cap);
        var visitors = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            visitors.Add(await SeedApprovedVisitorAsync());
        }

        var responses = await Task.WhenAll(visitors.Select(v =>
            PostArriveAsync(sessionId, CenterLat, CenterLon, v)));

        var success = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(cap, success);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.HallAtCapacity, body.Error!.Code);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var present = await db.HallAttendances
            .CountAsync(a => a.SessionId == sessionId && a.Leave == null);
        Assert.Equal(cap, present);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<HallAttendanceStatus> ArriveAsync(
        Guid sessionId, double lat, double lon, string token)
    {
        var response = await PostArriveAsync(sessionId, lat, lon, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<HallAttendanceStatus>>())!.Data!;
    }

    private Task<HttpResponseMessage> PostArriveAsync(
        Guid sessionId, double lat, double lon, string token) =>
        PostAuthAsync($"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = lat, Lon = lon }, token);

    private async Task<int> CountSessionRatingPromptsAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.SessionRatingRequest
            && n.RelatedEntityId == sessionId);
    }

    // Flips the seeded "Session" rating type active/inactive — the CP RatingConfig
    // toggle both session-rating-prompt producers now honour.
    private async Task SetSessionRatingTypeActiveAsync(bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await db.RatingTypes.SingleAsync(t => t.Code == "Session");
        type.IsActive = active;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedSessionAsync(
        bool withGeofence, int capacity = 100,
        int startOffsetMin = -15, int endOffsetMin = 45)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Attendance Hall", NameArabic = "قاعة الحضور",
            Capacity = capacity, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            GeofenceCenterLat = withGeofence ? CenterLat : null,
            GeofenceCenterLon = withGeofence ? CenterLon : null,
            GeofenceRadiusMeters = withGeofence ? RadiusMeters : null,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Attendance Session", TitleArabic = "جلسة الحضور",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddMinutes(startOffsetMin),
            End = DateTimeOffset.UtcNow.AddMinutes(endOffsetMin),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> SeedApprovedVisitorAsync()
    {
        var email = $"att-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Attendance Visitor",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
