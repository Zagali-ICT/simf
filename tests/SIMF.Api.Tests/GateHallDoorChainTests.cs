// X-1 / CHAIN-4 — a hall-door gate (Gate.HallId set) feeds HallAttendance: an
// allowed CheckIn opens the attendee's attendance row for the session live in
// that hall (Method=QrScan), a CheckOut closes it, a perimeter gate (HallId null)
// records only a GateScan, and a scan when no session is live records nothing.
// The DoD-critical assertion: HallAttendance.UserProfileId is bound to the
// attendee's App UserProfile.Id (QrResolution.UserProfileId), NEVER the Identity
// SimfUser.Id — an attendee need not hold an account, and a walk-in does not.
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
using SIMF.Domain.SeatReservations;
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
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // D-819 (step 11.5) — a session hall admits only a registered attendee.
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.HallAttendances
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();
        var row = Assert.Single(rows);
        // DoD-critical: the attendance row is keyed by the App UserProfile id, not
        // the Identity user id — an attendee need not have an account at all.
        Assert.Equal(attendeeProfileId, row.UserProfileId);
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
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // D-819 (step 11.5) — a session hall admits only a registered attendee.
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var checkIn = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        // A deliberate direction switch on a Both gate is NOT absorbed by the 5s
        // duplicate window, so the CheckOut is processed.
        var checkOut = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.HallAttendances
            .SingleAsync(a => a.SessionId == sessionId && a.UserProfileId == attendeeProfileId);
        Assert.NotNull(row.Leave);
    }

    [Fact]
    public async Task Hall_door_gate_with_no_live_session_records_no_attendance()
    {
        var (token, operatorUserId) = await CreateAdminAsync();
        var hallId = await SeedHallWithoutSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // D-819 (step 11.5) — a session hall admits only a registered attendee.
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Null(body.NoticeMessage);
    }

    [Fact]
    public async Task Session_hall_denies_an_attendee_who_is_not_registered_for_the_session()
    {
        // D-819 (gate engine step 11.5) — the third access rule: a MAIN gate
        // requires an approved account, ANY gate requires an allowed profile
        // type, and a SESSION HALL requires the attendee to be registered for
        // the session behind the door.
        //
        // This rule previously had no implementation at all:
        // DenialReasonCode.BookingRequiredMissing existed as a reserved hook
        // with no writer, so any valid badge opened every hall.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();
        // Deliberately NO seat reservation.

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        // A denial is a RECORDED outcome at HTTP 200, not an envelope failure.
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.BookingRequiredMissing, body.DenialReasonCode);

        // Denied entry must not record attendance.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, await db.HallAttendances.CountAsync(a => a.SessionId == sessionId));
    }

    [Fact]
    public async Task Admitted_attendee_gets_an_open_seating_hold_so_the_seating_desk_can_see_them()
    {
        // D-819 — without a hold the staff seating desk reports "no seat" for a
        // badge standing in front of it and the seat map under-reports the hall.
        // The hold carries no row/seat: it records THAT they are here, not WHERE,
        // which keeps it clear of the per-seat unique index and out of the
        // SERIALIZABLE seat-picking path that would deadlock a door rush.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var holds = await db.SeatReservations
            .Where(r => r.SessionId == sessionId
                && r.ReservedForProfileId == attendeeProfileId
                && r.ReleasedAt == null)
            .ToListAsync();

        // Exactly one: the attendee already held a reservation, so the walk-in
        // hold must not add a second.
        var hold = Assert.Single(holds);
        Assert.Null(hold.Expires);
    }

    [Fact]
    public async Task Perimeter_gate_admits_an_attendee_with_no_session_registration()
    {
        // D-819 — step 11.5 is a SESSION HALL rule. A perimeter gate has no
        // HallId, so venue entry must stay unaffected by session bookings.
        var (token, operatorUserId) = await CreateAdminAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId: null);
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
    }

    [Fact]
    public async Task Session_hall_never_blocks_someone_already_inside_from_leaving()
    {
        // D-819 — step 11.5 applies to ENTRIES only. Someone already inside must
        // always be able to leave, whatever their booking state.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, sessionId) = await SeedHallWithLiveSessionAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var checkIn = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        // Release the reservation while they are inside, then scan out.
        using (var releaseScope = _factory.Services.CreateScope())
        {
            var releaseDb = releaseScope.ServiceProvider
                .GetRequiredService<SimfAppDbContext>();
            var reservation = await releaseDb.SeatReservations
                .SingleAsync(r => r.SessionId == sessionId
                    && r.ReservedForProfileId == attendeeProfileId);
            reservation.ReleasedAt = SimfClock.Now;
            await releaseDb.SaveChangesAsync();
        }

        var checkOut = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        var body = (await checkOut.Content.ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
    }

    [Fact]
    public async Task Perimeter_gate_carries_no_notice()
    {
        // DEF-CHK-004 — a perimeter gate (HallId null) never feeds hall attendance,
        // so the "no attendance recorded" advisory must not fire there.
        var (token, operatorUserId) = await CreateAdminAsync();
        var gateId = await CreateGateAsync(token, operatorUserId, hallId: null);
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // D-819 (step 11.5) — a session hall admits only a registered attendee.
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

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
            .SingleAsync(a => a.SessionId == sessionId && a.UserProfileId == attendeeProfileId);
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
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // D-819 (step 11.5) — a session hall admits only a registered attendee.
        await SeedSeatReservationAsync(sessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.HallAttendances.SingleAsync(a => a.SessionId == sessionId);
        Assert.Equal(attendeeProfileId, row.UserProfileId);
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
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();

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
        var (_, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // 1st scan (inferred) — no open row → opens one.
        await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(1, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));

        // 2nd scan (inferred, SAME direction) — open row exists → closes it.
        await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
        Assert.Equal(0, await db.HallAttendances.CountAsync(
            a => a.SessionId == sessionId && a.Leave == null));

        // 3rd scan (inferred) — no open row again → opens a fresh one.
        await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);
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
        var (_, attendeeUserId, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
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
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: true, operatorId);

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
        var (_, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        var operatorId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IHallAttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: false, operatorId);
        await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn, directionInferred: false, operatorId);

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
        // The gate-door path takes the attendee PROFILE; the geofence path below
        // takes the signed-in ACCOUNT, so the second attendee is held as both.
        var (_, _, first) = await CreateApprovedVisitorWithQrAsync();
        var (_, secondUserId, second) = await CreateApprovedVisitorWithQrAsync();
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
            attendance.RecordGeofenceArrivalAsync(secondUserId, sessionId, CenterLat, CenterLon));
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
        var (_, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();

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
            services.GetRequiredService<
                Microsoft.Extensions.Options.IOptionsMonitor<
                    SIMF.Common.Options.WalkInModeOptions>>(),
            services.GetRequiredService<
                SIMF.Application.SeatReservations.Abstractions.ISeatReservationService>(),
            services.GetRequiredService<ILogger<HallAttendanceService>>());

        // A fixed In gate (directionInferred: false) keeps this on the arrival branch.
        var recorded = await attendance.RecordGateDoorScanAsync(
            attendeeProfileId, hallId, ScanDirection.CheckIn,
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

    [Fact]
    public async Task Session_hall_admits_a_booking_for_the_session_about_to_start()
    {
        // D-823 regression. Halls run back to back. Session A is running and
        // session B starts in 10 minutes; the attendee holds a booking for B and
        // is at the door now, which is exactly what the arrival grace exists to
        // allow. Step 11.5 used to ask only about the RUNNING session, so every
        // early arrival for B was denied BookingRequiredMissing — and widening
        // ArrivalGraceMinutes, the documented lever for a queue forming before a
        // keynote, widened the denial instead of the admission.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, _) = await SeedHallWithLiveSessionAsync(
            startOffsetMin: -50, endOffsetMin: 10);
        var nextSessionId = await SeedSessionInHallAsync(
            hallId, startOffsetMin: 10, endOffsetMin: 70);
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        // Booked for the NEXT session, not the one currently running.
        await SeedSeatReservationAsync(nextSessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
    }

    [Fact]
    public async Task Session_hall_still_refuses_a_booking_outside_the_admitting_window()
    {
        // The widening has a bound. A booking for a session three hours out is
        // not a ticket through the door now — otherwise "registered for that
        // session" would degrade into "holds any booking in this hall, ever".
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, _) = await SeedHallWithLiveSessionAsync(
            startOffsetMin: -15, endOffsetMin: 45);
        var farSessionId = await SeedSessionInHallAsync(
            hallId, startOffsetMin: 180, endOffsetMin: 240);
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, attendeeProfileId) = await CreateApprovedVisitorWithQrAsync();
        await SeedSeatReservationAsync(farSessionId, attendeeProfileId);

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.BookingRequiredMissing, body.DenialReasonCode);
    }

    [Fact]
    public async Task Session_hall_still_requires_a_booking_in_the_post_end_grace_tail()
    {
        // D-823 — widening step 11.5 to the whole admitting window must NOT turn
        // the tail after a session ends into an ungated door. The session is over
        // but still inside the arrival grace, so it is still what this hall is
        // admitting for and an unregistered badge is still refused. Dropping
        // ended sessions from the set would return NoLiveSession here, which the
        // gate does not deny on — every valid badge would walk in.
        var (token, operatorUserId) = await CreateAdminAsync();
        var (hallId, _) = await SeedHallWithLiveSessionAsync(
            startOffsetMin: -60, endOffsetMin: -5);
        var gateId = await CreateGateAsync(token, operatorUserId, hallId);
        var (qrId, _, _) = await CreateApprovedVisitorWithQrAsync();
        // Deliberately NO seat reservation.

        var scan = await PostScanAsync(gateId, qrId, token, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var body = (await scan.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.BookingRequiredMissing, body.DenialReasonCode);
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
            Start = SimfClock.Now.AddMinutes(startOffsetMin),
            End = SimfClock.Now.AddMinutes(endOffsetMin),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (hall.Id, session.Id);
    }

    /// <summary>D-823 — adds a SECOND session to an existing hall, so the
    /// back-to-back handover the arrival grace has to cope with can be
    /// exercised. A hall runs its programme without gaps; the interesting
    /// moment is the one where two sessions are both inside the window.</summary>
    private async Task<Guid> SeedSessionInHallAsync(
        Guid hallId, int startOffsetMin, int endOffsetMin)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Next Session", TitleArabic = "الجلسة التالية",
            HallId = hallId,
            Start = SimfClock.Now.AddMinutes(startOffsetMin),
            End = SimfClock.Now.AddMinutes(endOffsetMin),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    /// <summary>
    /// D-819 — registers an attendee for a session so a hall-door scan admits
    /// them. Step 11.5 makes "registered for this session" a real entry rule at
    /// a session hall; before D-819 it was a reserved hook with no writer, so
    /// any valid badge opened any hall and these chain tests needed no booking.
    /// </summary>
    private async Task SeedSeatReservationAsync(Guid sessionId, Guid attendeeProfileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ReservedForProfileId = attendeeProfileId,
            Kind = SeatReservationKind.OpenSeating,
            Status = BookingStatus.Approved,
            // The booking's AUTHOR stays an account; this fixture stands in for a
            // self-booking, so any account id serves and none is ever read back.
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
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
            Capacity = capacity, IsActive = true, CreatedAt = SimfClock.Now,
            GeofenceCenterLat = withGeofence ? CenterLat : null,
            GeofenceCenterLon = withGeofence ? CenterLon : null,
            GeofenceRadiusMeters = withGeofence ? RadiusMeters : null,
        };

    /// <summary>An approved attendee holding a badge. Returns the profile id
    /// alongside the account id because attendance and seating are keyed by the
    /// PROFILE — the account is only what they sign in with.</summary>
    private async Task<(string QrId, Guid UserId, Guid ProfileId)>
        CreateApprovedVisitorWithQrAsync()
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
        var profile = new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            NameArabic = "زائر السلسلة",
            Name = "Chain Visitor",
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            // Admission is decided on the PROFILE, not on the account, so a fixture
            // that approves only the SimfUser leaves an attendee every gate refuses.
            AdmissionState = AccountState.Approved,
            CreatedAt = SimfClock.Now,
        };
        appDb.UserProfiles.Add(profile);
        await appDb.SaveChangesAsync();
        return (qrId, user.Id, profile.Id);
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
        return (await AuthFlow.SignInControlPanelAsync(_client, _factory, email), userId);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
