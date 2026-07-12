// X-1 / CHAIN-4 — a hall-door gate (Gate.HallId set) feeds HallAttendance: an
// allowed CheckIn opens the attendee's attendance row for the session live in
// that hall (Method=QrScan), a CheckOut closes it, a perimeter gate (HallId null)
// records only a GateScan, and a scan when no session is live records nothing.
// The DoD-critical assertion: HallAttendance.UserId is bound to the attendee's
// Identity SimfUser.Id (QrResolution.UserId), NEVER the App UserProfile id.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class GateHallDoorChainTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // A fixed venue centre + a tight geofence, for the FIX D enforced-path arrival.
    private const double CenterLat = 24.7136;
    private const double CenterLon = 46.6753;
    private const double RadiusMeters = 100;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateHallDoorChainTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Allowed_checkin_on_a_hall_door_gate_opens_one_attendance_row_bound_to_the_identity_user_id()
    {
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.HallAttendances
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();
        var row = Assert.Single(rows);
        // DoD-critical: the attendance row is keyed by the Identity user id, not
        // the App UserProfile id.
        Assert.Equal(attendeeUserId, row.UserId);
        Assert.Equal(AttendanceMethod.QrScan, row.Method);
        Assert.Null(row.LeaveUtc);
    }

    [Fact]
    public async Task Perimeter_gate_records_no_attendance()
    {
        var (token, operatorUserId) = await CreateAdminAsync();
        var (_, sessionId) = await SeedHallWithLiveSessionAsync();
        // HallId null → a perimeter gate.
        var gateId = await CreateGateAsync(token, operatorUserId, hallId: null);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Checkout_on_a_hall_door_gate_closes_the_open_attendance_row()
    {
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();

        var checkIn = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        // A deliberate direction switch on a Both gate is NOT absorbed by the 5s
        // duplicate window, so the CheckOut is processed.
        var checkOut = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.HallAttendances
            .SingleAsync(a => a.SessionId == sessionId && a.UserId == attendeeUserId);
        Assert.NotNull(row.LeaveUtc);
    }

    [Fact]
    public async Task Hall_door_gate_with_no_live_session_records_no_attendance()
    {
        var (token, operatorUserId) = await CreateAdminAsync();
        var hallId = await SeedHallWithoutSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        // The gate scan itself still succeeds (Allowed); the chain records nothing.
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.HallId == hallId));
    }

    [Fact]
    public async Task Hall_door_gate_scan_within_grace_before_the_session_starts_records_attendance()
    {
        // FIX B — a session starting in 10 min is inside the ±15 min arrival grace,
        // so an early gate check-in binds to it and opens a row (the strict window
        // used to record nothing).
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync(
            startOffsetMin: 10, endOffsetMin: 70);
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.HallAttendances.SingleAsync(a => a.SessionId == sessionId);
        Assert.Equal(attendeeUserId, row.UserId);
        Assert.Null(row.LeaveUtc);
    }

    [Fact]
    public async Task Hall_door_gate_scan_outside_the_grace_window_records_nothing()
    {
        // FIX B — a session starting in 60 min is well outside the ±15 min grace,
        // so the gate scan records no attendance (the GateScan row still stands).
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync(
            startOffsetMin: 60, endOffsetMin: 120);
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Both_mode_gate_derives_the_action_from_attendance_state_not_direction()
    {
        // FIX C — on a Both-mode gate the passed direction is only an alternation
        // guess; the chain derives the real action from the open-row state. Three
        // scans with the SAME CheckIn direction must go open → close → open.
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var (_, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // 1st scan (inferred) — no open row → opens one.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.LeaveUtc == null));

        // 2nd scan (inferred, SAME direction) — open row exists → closes it.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(0, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.LeaveUtc == null));

        // 3rd scan (inferred) — no open row again → opens a fresh one.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.LeaveUtc == null));
        Assert.Equal(2, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Fixed_in_gate_scanned_twice_keeps_the_row_open()
    {
        // FIX C — a fixed In gate (directionInferred: false) is authoritative: two
        // CheckIn scans both take the arrival branch and merge into the one open
        // row; the row is never closed by the alternation.
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var (_, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: false, operatorId);
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: false, operatorId);

        Assert.Equal(1, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
        var row = await db.HallAttendances.SingleAsync(a => a.SessionId == sessionId);
        Assert.Null(row.LeaveUtc);
    }

    [Fact]
    public async Task Hall_door_gate_records_past_capacity_while_geofence_stays_hard_capped()
    {
        // FIX D — capacity is ADVISORY on the passive gate-door path (a turnstile
        // pass MUST be counted), but the operator/geofence arrival keeps the hard
        // HALL_AT_CAPACITY 409.
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync(
            capacity: 1, withGeofence: true);
        var (_, first) = await CreateApprovedVisitorWithQrAsync();
        var (_, second) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Fill the single-seat hall via the gate-door path.
        await attendance.RecordGateDoorScanAsync(
            first, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.LeaveUtc == null));

        // Enforced path (geofence) — hard 409 at capacity (unchanged).
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            attendance.RecordGeofenceArrivalAsync(second, sessionId, CenterLat, CenterLon));
        Assert.Equal(ErrorCodes.HallAtCapacity, ex.Code);

        // Advisory path (gate door) — records past the cap, does not throw.
        await attendance.RecordGateDoorScanAsync(
            second, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(2, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.LeaveUtc == null));
    }

    // -- Helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string token, ScanDirection direction)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new
            {
                qr,
                idempotencyKey = (string?)null,
                source = ScanSource.Simulator,
                direction,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<Guid> CreateGateAsync(string token, Guid operatorUserId, Guid? hallId)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"HD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Name = "Hall Door Gate",
                NameArabic = "بوابة باب القاعة",
                DirectionMode = DirectionMode.Both,
                HallId = hallId,
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = new List<Guid> { operatorUserId },
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
        Assert.Equal(hallId, detail.HallId);
        return detail.Id;
    }

    private async Task<(Guid HallId, Guid SessionId)> SeedHallWithLiveSessionAsync(
        int capacity = 100, bool withGeofence = false,
        int startOffsetMin = -15, int endOffsetMin = 45)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = NewHall(capacity, withGeofence);
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Chain Session", TitleArabic = "جلسة السلسلة",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(startOffsetMin),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(endOffsetMin),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (hall.Id, session.Id);
    }

    private async Task<Guid> SeedHallWithoutSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = NewHall();
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private static Hall NewHall(int capacity = 100, bool withGeofence = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Chain Hall", NameArabic = "قاعة السلسلة",
            Capacity = capacity, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            GeofenceCenterLat = withGeofence ? CenterLat : null,
            GeofenceCenterLon = withGeofence ? CenterLon : null,
            GeofenceRadiusMeters = withGeofence ? RadiusMeters : null,
        };

    private async Task<(string QrId, Guid UserId)> CreateApprovedVisitorWithQrAsync()
    {
        var email = $"chain-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Chain Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            NameArabic = "زائر السلسلة",
            Name = "Chain Visitor",
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return (qrId, user.Id);
    }

    private async Task<(string Token, Guid UserId)> CreateAdminAsync()
    {
        var email = $"chain-admin-{Guid.NewGuid():N}@simf.test";
        Guid userId;
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
                DisplayName = "Chain Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
