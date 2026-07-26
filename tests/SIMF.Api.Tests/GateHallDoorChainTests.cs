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
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Programme;
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
        Assert.Null(row.Leave);
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
        Assert.NotNull(row.Leave);
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
    public async Task Hall_door_gate_with_no_live_session_returns_an_allowed_scan_carrying_a_notice()
    {
        // DEF-CHK-004 — a hall-door scan outside every session window admits the
        // holder but records no attendance. That used to be silent (a plain
        // "Allowed"), so the attendance was lost with no signal. The scan is still
        // Allowed, but now carries the advisory NoticeMessage.
        var (token, operatorUserId) = await CreateAdminAsync();
        var hallId = await SeedHallWithoutSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        // The allow/deny outcome is unchanged — the person is admitted.
        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Null(body.DenialReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(body.NoticeMessage));
        Assert.Contains("attendance", body.NoticeMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hall_door_gate_bound_to_a_live_session_carries_no_notice()
    {
        // DEF-CHK-004 — the notice is the exception, not the norm: a scan that DID
        // bind to a live session records attendance and reports nothing extra.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, _) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Null(body.NoticeMessage);
    }

    [Fact]
    public async Task Perimeter_gate_carries_no_notice()
    {
        // DEF-CHK-004 — a perimeter gate (HallId null) never feeds hall attendance,
        // so the "no attendance recorded" advisory must not fire there.
        var (token, operatorUserId) = await CreateAdminAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId: null);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Null(body.NoticeMessage);
    }

    [Fact]
    public async Task Fixed_out_gate_with_no_open_row_carries_the_advisory_notice()
    {
        // DEF-CHK-004 (A4) — a fixed OUT gate is authoritative, so the chain takes
        // the departure branch. When the attendee has no open attendance row that
        // departure closes NOTHING, yet the chain used to report success and the
        // operator read a plain "Allowed" as "counted". The session IS live here, so
        // the only way a notice can appear is the honest check-out return value.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(
            token, operatorUserId, hallId, DirectionMode.Out);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(body.NoticeMessage));
        Assert.Contains("attendance", body.NoticeMessage!, StringComparison.OrdinalIgnoreCase);

        // Nothing was opened or closed — the advisory is telling the truth.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Fixed_out_gate_that_closes_an_open_row_carries_no_notice()
    {
        // DEF-CHK-004 (A4) — the counterpart: the SAME fixed OUT gate, but this time
        // there IS an open row, so the departure really is recorded and the operator
        // must NOT be warned. Guards the honest return value against over-reporting.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var inGateId = await CreateGateAsync(
            token, operatorUserId, hallId, DirectionMode.In);
        var outGateId = await CreateGateAsync(
            token, operatorUserId, hallId, DirectionMode.Out);
        var (qrId, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();

        var checkIn = await PostScanAsync(inGateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        var checkOut = await PostScanAsync(outGateId, qrId, token, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var body = (await checkOut.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Null(body.NoticeMessage);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.HallAttendances
            .SingleAsync(a => a.SessionId == sessionId && a.UserId == attendeeUserId);
        Assert.NotNull(row.Leave);
    }

    [Fact]
    public async Task Notice_is_Arabic_for_a_regional_ar_SA_accept_language()
    {
        // DEF-CHK-004 (A5) — the notice resolves language by an exact "ar" compare,
        // so a regional tag such as ar-SA (or a q-weighted list) would fall back to
        // English IF the raw header reached the service. It does not:
        // OperatorGateEndpoints normalises Accept-Language to exactly "ar" or "en"
        // before building the GateScanContext, and that endpoint is the ONLY
        // producer of the context. This test pins that normalisation so the strict
        // compare stays safe.
        var (token, operatorUserId) = await CreateAdminAsync();
        var hallId = await SeedHallWithoutSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(
            gateId, qrId, token, ScanDirection.CheckIn, acceptLanguage: "ar-SA,ar;q=0.9,en;q=0.8");
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.False(string.IsNullOrWhiteSpace(body.NoticeMessage));
        Assert.Contains("تم السماح بالدخول", body.NoticeMessage!, StringComparison.Ordinal);
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
        Assert.Null(row.Leave);
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
            a => a.SessionId == sessionId && a.Leave == null));

        // 2nd scan (inferred, SAME direction) — open row exists → closes it.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(0, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));

        // 3rd scan (inferred) — no open row again → opens a fresh one.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));
        Assert.Equal(2, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Both_mode_gate_scan_after_a_geofence_arrival_keeps_the_attendee_present()
    {
        // #24 — a row opened by the GPS geofence is a cross-channel arrival; the doc
        // merges every arrival means into the one open row, so the FIRST Both-mode
        // turnstile pass must MERGE, not close it. The old "any open row → departure"
        // rule closed the geofence row, marking a still-present attendee as departed
        // and under-counting live occupancy.
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync(withGeofence: true);
        var (_, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Arrival via geofence opens the one attendance row (Method=Geofence).
        await attendance.RecordGeofenceArrivalAsync(
            attendeeUserId, sessionId, CenterLat, CenterLon);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));

        // First Both-mode turnstile pass for the same attendee — must merge, not depart.
        await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);

        // Still present: the single open row remains open (merged), not closed.
        var row = await db.HallAttendances.SingleAsync(a => a.SessionId == sessionId);
        Assert.Null(row.Leave);
        Assert.Equal(AttendanceMethod.Geofence, row.Method);
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
        Assert.Null(row.Leave);
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
            a => a.SessionId == sessionId && a.Leave == null));

        // Enforced path (geofence) — hard 409 at capacity (unchanged).
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            attendance.RecordGeofenceArrivalAsync(second, sessionId, CenterLat, CenterLon));
        Assert.Equal(ErrorCodes.HallAtCapacity, ex.Code);

        // Advisory path (gate door) — records past the cap, does not throw.
        await attendance.RecordGateDoorScanAsync(
            second, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(2, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));
    }

    [Fact]
    public async Task Gate_door_arrival_that_persisted_no_row_does_not_report_attendance_recorded()
    {
        // DEF-CHK-004 (A4, round 3) — the ARRIVAL branch used to `return true`
        // unconditionally, so a scan whose insert never landed still told the gate
        // "attendance recorded" and the operator saw a plain "Allowed". The shared
        // create path swallows a DbUpdateException on the advisory (gate-door)
        // insert and hands back an UNSAVED row when no rival row can be re-read —
        // a deadlock victim, a command timeout, or the one-open-row race whose
        // rival has since closed. Nothing is then on the store for this attendee,
        // so the chain must report false and let the gate raise its advisory.
        // The failing SaveChanges is simulated at the DbContext boundary because
        // none of those store faults can be provoked deterministically against
        // LocalDB; every other query the service runs still hits the real database.
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var (_, attendeeUserId) = await CreateApprovedVisitorWithQrAsync();

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await using var failingDb = new AttendanceInsertFailsDbContext(
            services.GetRequiredService<DbContextOptions<SimfAppDbContext>>(),
            services.GetRequiredService<SIMF.Application.Abstractions.IPiiEncryptor>());
        var attendance = new HallAttendanceService(
            failingDb,
            services.GetRequiredService<IQrResolver>(),
            services.GetRequiredService<IAuditLog>(),
            services.GetRequiredService<INotificationDispatcher>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<HallAttendanceService>>());

        // A fixed In gate (directionInferred: false) keeps this on the arrival branch.
        var recorded = await attendance.RecordGateDoorScanAsync(
            attendeeUserId, hallId, ScanDirection.CheckIn,
            directionInferred: false, operatorUserId: Guid.NewGuid());

        Assert.False(recorded);
        var db = services.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Fails ONLY the attendance insert, with the <see cref="DbUpdateException"/>
    /// the service's own catch block is written against. Every other query — the
    /// live-session lookup, the capacity read, the post-failure open-row re-read —
    /// runs against the real database, so the service takes exactly the production
    /// path up to the store rejecting the write.</summary>
    private sealed class AttendanceInsertFailsDbContext(
        DbContextOptions<SimfAppDbContext> options,
        SIMF.Application.Abstractions.IPiiEncryptor pii)
        : SimfAppDbContext(options, pii)
    {
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<HallAttendance>().Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("Simulated store failure on the attendance insert.");
            }
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    private Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string token, ScanDirection direction,
        string? acceptLanguage = null)
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
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }
        return _client.SendAsync(request);
    }

    private async Task<Guid> CreateGateAsync(
        string token, Guid operatorUserId, Guid? hallId,
        DirectionMode directionMode = DirectionMode.Both)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"HD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Name = "Hall Door Gate",
                NameArabic = "بوابة باب القاعة",
                DirectionMode = directionMode,
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
            Start = DateTimeOffset.UtcNow.AddMinutes(startOffsetMin),
            End = DateTimeOffset.UtcNow.AddMinutes(endOffsetMin),
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
