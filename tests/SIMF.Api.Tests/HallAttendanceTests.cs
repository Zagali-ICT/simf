// P5.1b — D-241 (FDS-003 §5.4, FR-305/506): attendee-facing hall arrival via the
// GPS geofence. Covers inside/outside the geofence, the no-geofence hall, the
// idempotent re-arrival (one open row), departure, invalid coordinates, the
// pre-arrival status, and the anonymous rejection.
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
        Assert.NotNull(status.EnterUtc);
        Assert.Null(status.LeaveUtc);
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
        Assert.Equal(first.EnterUtc, second.EnterUtc);
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
        Assert.NotNull(status.LeaveUtc);
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

    private async Task<Guid> SeedSessionAsync(bool withGeofence)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Attendance Hall", NameArabic = "قاعة الحضور",
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
            Title = "Attendance Session", TitleArabic = "جلسة الحضور",
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
