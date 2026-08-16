// P5.1d — D-244 (FDS-003 §5.4): operator hall-door QR scan records a QrScan
// arrival. Covers the happy path, merge with a prior geofence arrival (one open
// row), unknown QR (400), non-approved attendee (403), and a non-operator (403).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class HallArrivalScanTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const double CenterLat = 24.7136;
    private const double CenterLon = 46.6753;
    private const double RadiusMeters = 100;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public HallArrivalScanTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Operator_scan_records_a_qr_arrival()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, userId, _, _, _) = await CreateApprovedVisitorWithQrAsync(approved: true);

        var result = await ScanAsync(sessionId, qrId, operatorToken);
        Assert.Equal(userId, result.UserId);
        Assert.True(result.Status.Arrived);
        Assert.Equal(AttendanceMethod.QrScan, result.Status.Method);
    }

    [Fact]
    public async Task Scan_merges_with_a_prior_geofence_arrival_one_open_row()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: true);
        var (qrId, _, visitorToken, _, profileId) = await CreateApprovedVisitorWithQrAsync(approved: true);

        // Attendee arrives via GPS first.
        var arrival = await PostAuthAsync($"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = CenterLat, Lon = CenterLon }, visitorToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);

        // Operator then scans the same attendee — merges into the one open row.
        var result = await ScanAsync(sessionId, qrId, operatorToken);
        Assert.True(result.Status.Arrived);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var count = await db.HallAttendances
            .CountAsync(a => a.SessionId == sessionId && a.UserProfileId == profileId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Unknown_qr_is_400()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);

        var response = await PostScanAsync(sessionId,
            Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), operatorToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_approved_attendee_is_403()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, _, _, _, _) = await CreateApprovedVisitorWithQrAsync(approved: false);

        var response = await PostScanAsync(sessionId, qrId, operatorToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_non_operator_account_is_forbidden()
    {
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, _, visitorToken, _, _) = await CreateApprovedVisitorWithQrAsync(approved: true);

        // An approved visitor lacks HallArrivals.Record.
        var response = await PostScanAsync(sessionId, qrId, visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- 2026-07-18: staff check-OUT (operator QR departure scan) --------------

    [Fact]
    public async Task Operator_scan_records_a_departure()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, userId, _, _, profileId) = await CreateApprovedVisitorWithQrAsync(approved: true);

        // Check the attendee in first.
        var arrival = await ScanAsync(sessionId, qrId, operatorToken);
        Assert.True(arrival.Status.Arrived);

        // Staff scans the badge at the departures endpoint — checked OUT.
        var departure = await DepartAsync(sessionId, qrId, operatorToken);
        Assert.Equal(userId, departure.UserId);
        Assert.False(departure.Status.Arrived);
        Assert.NotNull(departure.Status.Leave);

        // No open row remains — the seat map's confirmed state clears.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var open = await db.HallAttendances
            .CountAsync(a => a.SessionId == sessionId && a.UserProfileId == profileId && a.Leave == null);
        Assert.Equal(0, open);
    }

    [Fact]
    public async Task Departure_without_a_prior_arrival_is_an_idempotent_noop()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, _, _, _, _) = await CreateApprovedVisitorWithQrAsync(approved: true);

        // No check-in first — the check-out is a 200 no-op (Arrived=false).
        var departure = await DepartAsync(sessionId, qrId, operatorToken);
        Assert.False(departure.Status.Arrived);
    }

    [Fact]
    public async Task Unknown_qr_departure_is_400()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);

        var response = await PostDepartAsync(sessionId,
            Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), operatorToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_non_operator_cannot_record_a_departure()
    {
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, _, visitorToken, _, _) = await CreateApprovedVisitorWithQrAsync(approved: true);

        var response = await PostDepartAsync(sessionId, qrId, visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- DEF-CHK-002: per-session rating IS reachable without the geofence -------

    [Fact]
    public async Task Operator_scan_makes_the_per_session_rating_submittable()
    {
        // DEF-CHK-002 (refutation) — the audit claimed the per-session rating is
        // unreachable because RatingFormService requires a HallAttendance row
        // "nothing in the product creates automatically". It does: the operator's
        // hall-door QR scan writes one, keyed on the SAME attendee profile the
        // rating gate resolves the `sub` claim to. So the rating unlocks with no
        // geofence involved (the deferred D-211 self-service path).
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(withGeofence: false);
        var (qrId, _, visitorToken, _, _) = await CreateApprovedVisitorWithQrAsync(approved: true);

        // Before any check-in the gate refuses the submission.
        var blocked = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Session", TargetId = sessionId, OverallStars = 4 },
            visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var blockedBody = (await blocked.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RatingNotAttended, blockedBody.Error!.Code);

        // The operator scans them in at the hall door — that alone unlocks it.
        var arrival = await ScanAsync(sessionId, qrId, operatorToken);
        Assert.True(arrival.Status.Arrived);

        var allowed = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Session", TargetId = sessionId, OverallStars = 4 },
            visitorToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var view = (await allowed.Content.ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;
        Assert.Equal(sessionId, view.TargetId);
        Assert.Equal(4, view.OverallStars);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<QrArrivalResult> ScanAsync(Guid sessionId, string qrId, string token)
    {
        var response = await PostScanAsync(sessionId, qrId, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
    }

    private Task<HttpResponseMessage> PostScanAsync(Guid sessionId, string qrId, string token) =>
        PostAuthAsync($"/api/v1/admin/sessions/{sessionId}/arrivals",
            new RecordQrArrivalRequest { QrId = qrId }, token);

    private async Task<QrArrivalResult> DepartAsync(Guid sessionId, string qrId, string token)
    {
        var response = await PostDepartAsync(sessionId, qrId, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
    }

    private Task<HttpResponseMessage> PostDepartAsync(Guid sessionId, string qrId, string token) =>
        PostAuthAsync($"/api/v1/admin/sessions/{sessionId}/departures",
            new RecordQrArrivalRequest { QrId = qrId }, token);

    private async Task<Guid> SeedSessionAsync(bool withGeofence)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Scan Hall", NameArabic = "قاعة المسح",
            Capacity = 100, IsActive = true, CreatedAt = SimfClock.Now,
            GeofenceCenterLat = withGeofence ? CenterLat : null,
            GeofenceCenterLon = withGeofence ? CenterLon : null,
            GeofenceRadiusMeters = withGeofence ? RadiusMeters : null,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Scan Session", TitleArabic = "جلسة المسح",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(-15),
            End = SimfClock.Now.AddMinutes(45),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    /// <summary>An attendee with a badge. Returns the PROFILE id as well as the
    /// account id, because attendance rows are keyed by the profile — the account
    /// is only what they sign in to the app with.</summary>
    private async Task<(string QrId, Guid UserId, string Token, string DisplayName,
        Guid ProfileId)> CreateApprovedVisitorWithQrAsync(bool approved)
    {
        var email = $"scan-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var displayName = "Scan Visitor";
        Guid userId;
        Guid profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = displayName,
                AccountState = approved ? AccountState.Approved : AccountState.PendingApproval,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = new SIMF.Domain.Profiles.UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                QrId = qrId,
                NameArabic = "زائر المسح",
                Name = displayName,
                NationalityId = 682,
                PlaceOfBirth = "Riyadh",
                // The door reads admission from the PROFILE, so approving only the
                // account would leave every scan of this badge refused.
                AdmissionState = approved
                    ? AccountState.Approved
                    : AccountState.PendingApproval,
                CreatedAt = SimfClock.Now,
            };
            appDb.UserProfiles.Add(profile);
            await appDb.SaveChangesAsync();
            profileId = profile.Id;
        }

        var token = string.Empty;
        if (approved)
        {
            var sign = await _client.PostAsJsonAsync(
                "/api/v1/app/auth/sign-in",
                new SignInRequest { Email = email, Password = AuthFlow.Password });
            var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
            token = body.Data!.Tokens!.AccessToken;
        }
        return (qrId, userId, token, displayName, profileId);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"scan-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Scan Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
