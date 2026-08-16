// FR-1103 (register defect `FR-1103-movement-dwell`) — movement / dwell / route
// tracking had NO capture path and no data source: a repo-wide grep for
// dwell|MovementTrack returned only two comments saying the feature was deferred.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the FR-1103 capture path, the dwell-per-hall aggregation
/// and the route projection — including the "inert until a hall has a boundary"
/// guarantee that made Q6 buildable without the venue-boundary decision.
/// </summary>
public sealed class MovementTrackingTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // A point inside the seeded hall boundary, and one ~1.1 km away (outside a
    // 150 m radius).
    private const double HallLat = 24.7136;
    private const double HallLon = 46.6753;
    private const double AwayLat = 24.7236;
    private const double AwayLon = 46.6753;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MovementTrackingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Pings_inside_a_configured_boundary_bind_to_that_hall()
    {
        var hallId = await SeedHallWithGeofenceAsync();
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var start = SimfClock.Now.AddMinutes(-30);

        var response = await PostAuthAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest
            {
                Samples =
                [
                    new(start, HallLat, HallLon, 8),
                    new(start.AddMinutes(5), HallLat, HallLon, 8),
                    new(start.AddMinutes(10), AwayLat, AwayLon, 12),
                ],
            }, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecordDevicePositionsResponse>>())!.Data!;
        Assert.Equal(3, body.Accepted);
        // Two inside the boundary, one well outside it.
        Assert.Equal(2, body.MatchedToHall);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = db.DevicePositionPings.Where(p => p.HallId == hallId).ToList();
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task A_hall_with_no_boundary_matches_nothing()
    {
        // Q6's whole premise: the capture path ships now and stays dormant until a
        // hall is given lat/lon/radius from the CP. A ping taken standing INSIDE an
        // un-bounded hall is stored and matches nothing — no error, no partial
        // feature, just silence.
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var (_, lat, lon) = await SeedHallWithoutGeofenceAsync();
        var at = SimfClock.Now.AddMinutes(-5);

        var response = await PostAuthAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest
            {
                Samples = [new(at, lat, lon, 8)],
            }, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecordDevicePositionsResponse>>())!.Data!;
        Assert.Equal(1, body.Accepted);
        Assert.Equal(0, body.MatchedToHall);
    }

    // Lat/lon were range-checked but the accuracy radius was stored verbatim, so a
    // device (or a hand-rolled caller) could post a negative or absurd radius into
    // a column the dwell/route reads will later have to trust.
    [Fact]
    public async Task A_negative_accuracy_radius_is_rejected()
    {
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest
            {
                Samples = [new(SimfClock.Now.AddMinutes(-1), HallLat, HallLon, -5)],
            }, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    [Fact]
    public async Task An_absurd_accuracy_radius_is_rejected()
    {
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest
            {
                Samples = [new(SimfClock.Now.AddMinutes(-1), HallLat, HallLon, 250_000)],
            }, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_sample_with_no_accuracy_is_still_accepted()
    {
        // The field is optional on the wire — a device that reports no accuracy
        // must keep working, so the new bound must not turn null into a 400.
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest
            {
                Samples = [new(SimfClock.Now.AddMinutes(-1), HallLat, HallLon)],
            }, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dwell_aggregation_sums_time_between_consecutive_pings_in_a_hall()
    {
        var hallId = await SeedHallWithGeofenceAsync();
        var userId = Guid.NewGuid();
        var start = SimfClock.Now.AddHours(-2);

        // 0, +5, +10 minutes inside the hall = 10 minutes of dwell for one attendee.
        await SeedPingsAsync(userId, hallId,
            [start, start.AddMinutes(5), start.AddMinutes(10)]);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMovementTrackingService>();
        var dwell = await service.DwellByHallAsync(start.AddMinutes(-1), start.AddHours(1));

        var hall = Assert.Single(dwell, d => d.HallId == hallId);
        Assert.Equal(1, hall.DistinctAttendees);
        Assert.Equal(10d, hall.TotalDwellMinutes, 1);
        Assert.Equal(10d, hall.AverageDwellMinutes, 1);
    }

    [Fact]
    public async Task Dwell_aggregation_does_not_count_a_long_silence_as_presence()
    {
        // A device that went dark for an hour did not spend that hour in the hall.
        var hallId = await SeedHallWithGeofenceAsync();
        var userId = Guid.NewGuid();
        var start = SimfClock.Now.AddHours(-5);

        await SeedPingsAsync(userId, hallId, [start, start.AddHours(1)]);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMovementTrackingService>();
        var dwell = await service.DwellByHallAsync(start.AddMinutes(-1), start.AddHours(2));

        var hall = Assert.Single(dwell, d => d.HallId == hallId);
        Assert.Equal(1, hall.DistinctAttendees);
        Assert.Equal(0d, hall.TotalDwellMinutes);
    }

    [Fact]
    public async Task Route_projection_collapses_the_track_into_ordered_legs()
    {
        var hallId = await SeedHallWithGeofenceAsync();
        var userId = Guid.NewGuid();
        var start = SimfClock.Now.AddHours(-3);

        // In the hall, then outside it (the walk), then back in.
        await SeedPingsAsync(userId, hallId, [start, start.AddMinutes(4)]);
        await SeedPingsAsync(userId, null, [start.AddMinutes(8)]);
        await SeedPingsAsync(userId, hallId, [start.AddMinutes(12), start.AddMinutes(16)]);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMovementTrackingService>();
        var route = await service.RouteForAttendeeAsync(
            userId, start.AddMinutes(-1), start.AddHours(1));

        Assert.Equal(3, route.Legs.Count);
        Assert.Equal(hallId, route.Legs[0].HallId);
        Assert.Null(route.Legs[1].HallId);
        Assert.Equal(hallId, route.Legs[2].HallId);
        // Legs are ordered by entry.
        Assert.True(route.Legs[0].Enter < route.Legs[1].Enter);
        Assert.True(route.Legs[1].Enter < route.Legs[2].Enter);
        Assert.Equal(4d, route.Legs[0].DwellMinutes, 1);
    }

    [Fact]
    public async Task Movement_reports_require_the_attendance_view_permission()
    {
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var from = Uri.EscapeDataString(SimfClock.Now.AddHours(-1).ToString("O"));
        var to = Uri.EscapeDataString(SimfClock.Now.ToString("O"));

        var dwell = await GetAuthAsync(
            $"/api/v1/admin/movement/dwell?from={from}&to={to}", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, dwell.StatusCode);

        var route = await GetAuthAsync(
            $"/api/v1/admin/movement/route/{Guid.NewGuid()}?from={from}&to={to}",
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, route.StatusCode);
    }

    [Fact]
    public async Task Reporting_window_is_required_and_bounded()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var missing = await GetAuthAsync("/api/v1/admin/movement/dwell", token);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var from = Uri.EscapeDataString(SimfClock.Now.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(SimfClock.Now.ToString("O"));
        var tooWide = await GetAuthAsync(
            $"/api/v1/admin/movement/dwell?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.BadRequest, tooWide.StatusCode);
    }

    [Fact]
    public async Task Capture_endpoint_requires_an_approved_account()
    {
        var anonymous = await _client.PostAsJsonAsync("/api/v1/app/movement/pings",
            new RecordDevicePositionsRequest { Samples = [] });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedHallWithGeofenceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = $"MV{Guid.NewGuid():N}"[..8],
            Name = "Movement Test Hall",
            NameArabic = "قاعة اختبار الحركة",
            Capacity = 100,
            GeofenceCenterLat = HallLat,
            GeofenceCenterLon = HallLon,
            GeofenceRadiusMeters = 150,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    /// <summary>A hall with NO geofence configured, and a coordinate far from every
    /// other test hall's boundary — the shipped state Q6 leaves the feature in.
    /// Deliberately does not mutate the shared fixture's other halls.</summary>
    private async Task<(Guid HallId, double Lat, double Lon)> SeedHallWithoutGeofenceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = $"NB{Guid.NewGuid():N}"[..8],
            Name = "Unbounded Hall",
            NameArabic = "قاعة بلا حدود",
            Capacity = 100,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        // Jeddah, ~850 km from the Riyadh coordinates every bounded test hall uses.
        return (hall.Id, 21.4858, 39.1925);
    }

    /// <summary>Seeds pings directly, so the aggregation tests do not depend on the
    /// geofence resolution the capture tests already cover.</summary>
    private async Task SeedPingsAsync(
        Guid userId, Guid? hallId, IReadOnlyList<DateTime> capturedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        foreach (var at in capturedAt)
        {
            db.DevicePositionPings.Add(new DevicePositionPing
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HallId = hallId,
                CapturedAt = at,
                Latitude = hallId is null ? AwayLat : HallLat,
                Longitude = hallId is null ? AwayLon : HallLon,
                CreatedAt = SimfClock.Now,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"fr1103-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Movement Report Admin",
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
}
