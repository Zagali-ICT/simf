// Increment 1c — an attendee with NO Identity account is the ordinary case: a
// walk-in registered at the desk and a badge minted into a bulk order both produce
// a UserProfile with a null UserId. Attendance, seating and exhibitor leads used to
// be keyed by the ACCOUNT id, so such a person could not be recorded at a hall
// door, could not hold the seat they were standing in, and answered "no visitor
// badge matches this code" at a booth. All three are now keyed by the PROFILE id,
// which every attendee has.
//
// The fourth test is the invariant the re-key could most easily have broken: a
// geofence arrival (which knows the signed-in ACCOUNT) and a door scan (which
// resolves the badge to the PROFILE) must still land on the SAME open row.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Programme.Abstractions;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AccountlessAttendeeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const double CenterLat = 24.7136;
    private const double CenterLon = 46.6753;
    private const double RadiusMeters = 100;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AccountlessAttendeeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_attendee_with_no_account_is_recorded_at_a_hall_door()
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var (sessionId, _) = await SeedLiveSessionAsync(withGeofence: false);
        var qrId = TestAttendeeProfiles.NewQrId();
        var profileId = await TestAttendeeProfiles.CreateAccountlessAsync(_factory, qrId);

        var response = await PostAuthAsync(
            $"/api/v1/admin/sessions/{sessionId}/arrivals",
            new RecordQrArrivalRequest { QrId = qrId }, operatorToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;

        Assert.True(result.Status.Arrived);
        Assert.Equal(AttendanceMethod.QrScan, result.Status.Method);
        // The appended field carries the attendee; the SHIPPED UserId field is
        // empty because there is no account, and must never be looked up by.
        Assert.Equal(profileId, result.UserProfileId);
        Assert.Equal(Guid.Empty, result.UserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = Assert.Single(await db.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId).ToListAsync());
        Assert.Equal(profileId, row.UserProfileId);
        Assert.Null(row.Leave);
    }

    [Fact]
    public async Task An_attendee_with_no_account_can_hold_a_seat_and_be_found_at_the_seating_desk()
    {
        var (sessionId, _) = await SeedLiveSessionAsync(withGeofence: false);
        var qrId = TestAttendeeProfiles.NewQrId();
        var profileId = await TestAttendeeProfiles.CreateAccountlessAsync(_factory, qrId);
        var operatorUserId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var seats = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var recorded = await seats.EnsureWalkInHoldAsync(sessionId, profileId, operatorUserId);
        Assert.True(recorded);

        var hold = Assert.Single(await db.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId).ToListAsync());
        Assert.Equal(profileId, hold.ReservedForProfileId);
        // The AUTHOR is the operator who admitted them, not the attendee: the
        // creator column is an Identity account and a walk-in holds none.
        Assert.Equal(operatorUserId, hold.CreatedByUserId);
        // Never released by the no-show sweep — they are physically present.
        Assert.Null(hold.NoShowReleaseAt);

        // The seating desk resolves the same badge to that hold. This is what used
        // to answer "no seat" for the very badge the hold was just created for.
        var occupant = await seats.ResolveBadgeSeatAsync(sessionId, qrId);
        Assert.True(occupant.Found);
        Assert.Equal(hold.Id, occupant.ReservationId);
        Assert.Equal(profileId, occupant.UserProfileId);
        // The shipped UserId field is null (no account), so no photo can be served,
        // but the desk still gets a real name off the profile.
        Assert.Null(occupant.UserId);
        Assert.False(occupant.HasPhoto);
        Assert.Equal("Attendee", occupant.DisplayName);
    }

    [Fact]
    public async Task An_attendee_with_no_account_can_be_captured_as_an_exhibitor_lead()
    {
        var qrId = TestAttendeeProfiles.NewQrId();
        var visitorProfileId = await TestAttendeeProfiles.CreateAccountlessAsync(_factory, qrId);
        var (exhibitorToken, exhibitorUserId) = await CreateBoothOfficerAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = qrId, Note = "Met at the booth" },
            exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var card = (await response.Content.ReadFromJsonAsync<ApiResult<VisitorCard>>())!.Data!;

        // The lead resolves fully: a real name off the profile, no email (that
        // needs an account), and identified by profile rather than by account.
        Assert.True(card.Available);
        Assert.Equal("Attendee", card.Name);
        Assert.Null(card.Email);
        Assert.Equal(visitorProfileId, card.UserProfileId);
        Assert.Equal(Guid.Empty, card.UserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var capture = Assert.Single(await db.ExhibitorVisitorScans.AsNoTracking()
            .Where(s => s.ExhibitorUserId == exhibitorUserId).ToListAsync());
        Assert.Equal(visitorProfileId, capture.VisitorProfileId);
        Assert.Equal("Met at the booth", capture.Note);
    }

    [Fact]
    public async Task A_geofence_arrival_and_a_door_scan_still_merge_into_one_open_row()
    {
        // The two paths reach the attendee by different routes — the geofence from
        // the signed-in ACCOUNT, the door from the badge's PROFILE — so this is the
        // test that proves both translations land on the same attendee. If they
        // ever diverge the merge silently becomes two rows and live occupancy
        // double-counts every attendee who does both.
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var (sessionId, _) = await SeedLiveSessionAsync(withGeofence: true);
        var (qrId, visitorToken, profileId) = await CreateApprovedVisitorWithAccountAsync();

        var geofence = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/arrival",
            new RecordArrivalRequest { Lat = CenterLat, Lon = CenterLon }, visitorToken);
        Assert.Equal(HttpStatusCode.OK, geofence.StatusCode);

        var scan = await PostAuthAsync(
            $"/api/v1/admin/sessions/{sessionId}/arrivals",
            new RecordQrArrivalRequest { QrId = qrId }, operatorToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var result = (await scan.Content
            .ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
        Assert.Equal(profileId, result.UserProfileId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = Assert.Single(await db.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId).ToListAsync());
        Assert.Equal(profileId, row.UserProfileId);
        // The geofence opened it, so the door scan merged rather than re-opening.
        Assert.Equal(AttendanceMethod.Geofence, row.Method);
        Assert.Null(row.Leave);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(Guid SessionId, Guid HallId)> SeedLiveSessionAsync(bool withGeofence)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Walk-in Hall", NameArabic = "قاعة الدخول المباشر",
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
            Title = "Walk-in Session", TitleArabic = "جلسة الدخول المباشر",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(-15),
            End = SimfClock.Now.AddMinutes(45),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session.Id, hall.Id);
    }

    /// <summary>An attendee who DOES hold an account, for the merge test — both
    /// the app token and the badge resolve to the one profile.</summary>
    private async Task<(string QrId, string Token, Guid ProfileId)>
        CreateApprovedVisitorWithAccountAsync()
    {
        var email = $"merge-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = TestAttendeeProfiles.NewQrId();
        Guid profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Merge Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                QrId = qrId,
                Name = "Merge Visitor", NameArabic = "زائر الدمج",
                NationalityId = 682,
                AdmissionState = AccountState.Approved,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.UserProfiles.Add(profile);
            await appDb.SaveChangesAsync();
            profileId = profile.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SIMF.Contracts.Authentication.SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
            });
        var body = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SIMF.Contracts.Authentication.SignInResponse>>())!;
        return (qrId, body.Data!.Tokens!.AccessToken, profileId);
    }

    /// <summary>A signed-in exhibitor officer with a current booth membership —
    /// both halves of the authorisation the scan endpoint requires.</summary>
    private async Task<(string Token, Guid UserId)> CreateBoothOfficerAsync()
    {
        var email = $"booth-officer-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Booth Officer",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profileType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Exhibitor " + Guid.NewGuid().ToString("N")[..8],
                NameArabic = "عارض",
                PageColor = "#0EA5E9",
                IsForVisitor = false,
                MobileAppRole = MobileAppRole.Exhibitor,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.ProfileTypes.Add(profileType);
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = profileType.Id,
                Name = "Booth Officer", NameArabic = "مسؤول الجناح",
                NationalityId = 682,
                AdmissionState = AccountState.Approved,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            var exhibitor = new Exhibitor
            {
                Id = Guid.NewGuid(),
                Name = "Maritime Systems", NameArabic = "الأنظمة البحرية",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.Exhibitors.Add(exhibitor);
            appDb.ExhibitorMemberships.Add(new ExhibitorMembership
            {
                Id = Guid.NewGuid(),
                ExhibitorId = exhibitor.Id,
                UserId = userId,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SIMF.Contracts.Authentication.SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
            });
        var body = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SIMF.Contracts.Authentication.SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"walkin-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Walk-in Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync(string url, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
