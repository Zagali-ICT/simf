// P5.1a — D-240 (FDS-003 §5.4 + OI-2): the optional per-hall GPS geofence
// (centre lat/lon + radius). Covers the create/update round-trip, the
// ship-empty default (all null), and the all-or-nothing + range validation.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminHallGeofenceTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminHallGeofenceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_with_a_valid_geofence_round_trips_the_coordinates()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var detail = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Geo Hall", NameArabic = "قاعة جغرافية", Capacity = 100,
            GeofenceCenterLat = 24.7136, GeofenceCenterLon = 46.6753, GeofenceRadiusMeters = 75,
        });

        Assert.Equal(24.7136, detail.GeofenceCenterLat);
        Assert.Equal(46.6753, detail.GeofenceCenterLon);
        Assert.Equal(75, detail.GeofenceRadiusMeters);
    }

    [Fact]
    public async Task Create_without_a_geofence_leaves_all_three_null()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var detail = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Plain Hall", NameArabic = "قاعة عادية", Capacity = 50,
        });

        Assert.Null(detail.GeofenceCenterLat);
        Assert.Null(detail.GeofenceCenterLon);
        Assert.Null(detail.GeofenceRadiusMeters);
    }

    [Fact]
    public async Task Create_with_a_partial_geofence_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync("/api/v1/admin/halls", new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Partial", NameArabic = "جزئي", Capacity = 10,
            GeofenceCenterLat = 24.7, // lon + radius missing
        }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_an_out_of_range_radius_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync("/api/v1/admin/halls", new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "BadRadius", NameArabic = "نصف قطر خاطئ", Capacity = 10,
            GeofenceCenterLat = 24.7, GeofenceCenterLon = 46.6, GeofenceRadiusMeters = 0,
        }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_can_clear_a_previously_set_geofence()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var created = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Clearable", NameArabic = "قابلة للمسح", Capacity = 20,
            GeofenceCenterLat = 24.7, GeofenceCenterLon = 46.6, GeofenceRadiusMeters = 50,
        });

        var response = await PutAuthAsync($"/api/v1/admin/halls/{created.Id}", new AdminUpdateHallRequest
        {
            Code = created.Code, Name = created.Name, NameArabic = created.NameArabic,
            Capacity = created.Capacity, IsActive = true,
            // geofence omitted -> all null -> cleared
        }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        Assert.Null(updated.GeofenceCenterLat);
        Assert.Null(updated.GeofenceCenterLon);
        Assert.Null(updated.GeofenceRadiusMeters);
    }

    [Fact]
    public async Task Update_preserves_a_geofence_that_is_resent()
    {
        // D-505 regression — the inline UpdateHallRequest bind model omitted the
        // geofence, so FastEndpoints dropped the geofence the CP form resends and
        // UpdateAsync wiped the stored geofence on every edit. Resending it (while
        // changing another field) must round-trip. This drives the HTTP bind layer
        // — the service maps it correctly, so only an HTTP test catches the drop.
        var admin = await CreateAdministratorAndSignInAsync();
        var created = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Keep Geo", NameArabic = "احتفظ بالسياج", Capacity = 30,
            GeofenceCenterLat = 24.7136, GeofenceCenterLon = 46.6753, GeofenceRadiusMeters = 80,
        });

        var response = await PutAuthAsync($"/api/v1/admin/halls/{created.Id}",
            new AdminUpdateHallRequest
            {
                Code = created.Code, Name = created.Name, NameArabic = created.NameArabic,
                Capacity = 45, IsActive = true,
                GeofenceCenterLat = 24.7136, GeofenceCenterLon = 46.6753, GeofenceRadiusMeters = 80,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        Assert.Equal(45, updated.Capacity);
        Assert.Equal(24.7136, updated.GeofenceCenterLat);
        Assert.Equal(46.6753, updated.GeofenceCenterLon);
        Assert.Equal(80, updated.GeofenceRadiusMeters);
    }

    [Fact]
    public async Task Create_and_update_round_trip_the_seat_selection_mode()
    {
        // D-485 — the hall's seat-selection mode persists and round-trips. The
        // default (omitted) is AssignedSeat (0); create open, then update back.
        var admin = await CreateAdministratorAndSignInAsync();

        var assigned = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Assigned", NameArabic = "محدد", Capacity = 100,
        });
        Assert.Equal(0, assigned.SeatSelectionMode); // default AssignedSeat

        var open = await CreateHallAsync(admin, new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Open", NameArabic = "مفتوح", Capacity = 100,
            SeatSelectionMode = 1, // OpenSeating
        });
        Assert.Equal(1, open.SeatSelectionMode);

        var response = await PutAuthAsync($"/api/v1/admin/halls/{open.Id}",
            new AdminUpdateHallRequest
            {
                Code = open.Code, Name = open.Name, NameArabic = open.NameArabic,
                Capacity = open.Capacity, IsActive = true,
                SeatSelectionMode = 0, // back to AssignedSeat
            }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        Assert.Equal(0, updated.SeatSelectionMode);
    }

    [Fact]
    public async Task Create_with_an_invalid_seat_selection_mode_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync("/api/v1/admin/halls", new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "BadMode", NameArabic = "وضع خاطئ", Capacity = 10,
            SeatSelectionMode = 99,
        }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "GH" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<AdminHallDetail> CreateHallAsync(string token, AdminCreateHallRequest request)
    {
        var response = await PostAuthAsync("/api/v1/admin/halls", request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"hall-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Hall Admin",
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
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
