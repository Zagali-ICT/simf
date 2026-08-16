// Tests cover the 13-step constraint engine in GateOperatorService:
// step 2 (operator not assigned → 403); step 3 (QR_UNKNOWN); step 6
// (HOLDER_NOT_APPROVED); step 11 (PROFILE_TYPE_NOT_ALLOWED + L-15 empty-
// filtered-list denies all); step 12 (5-second duplicate absorption + Both-
// mode direction inference); §9 idempotency replay + 409 conflict.
// D-148 (Gate Module, SIMF-FDS-003 §5.6, SIMF-API-GATES-001).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GateScanTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateScanTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QR_unknown_records_a_denial_with_QR_UNKNOWN()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        var response = await PostScanAsync(gate.Id,
            qr: "ZZZZ99999999", token, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.QrUnknown, body.DenialReasonCode);
        Assert.Null(body.UserProfile);
    }

    [Fact]
    public async Task Over_length_QR_records_a_QR_UNKNOWN_denial_not_a_500()
    {
        // #14 — GateScan.QrIdAtScan is nvarchar(32); a normalised QR longer than the
        // column (a URL / WiFi / vCard QR someone mis-scans, or manual free-text
        // entry) must be DENIED as QrUnknown at HTTP 200, not truncate on insert and
        // surface as a 500.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        // 42 chars after normalise (Trim + ToUpper) — over the 32-char column.
        const string overLengthQr = "HTTPS://EXAMPLE.COM/SOME/LONG/PATH?X=12345";
        var response = await PostScanAsync(gate.Id, qr: overLengthQr, token, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.QrUnknown, body.DenialReasonCode);
        Assert.Null(body.UserProfile);
    }

    [Fact]
    public async Task Operator_not_assigned_returns_403_GATE_OPERATOR_NOT_ASSIGNED()
    {
        var (adminA, _) = await CreateAdminAsync();
        var (adminB, _) = await CreateAdminAsync();

        // admin A creates a gate but only assigns themselves
        var gate = await CreateGateAsync(adminA, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        // admin B tries to scan — passes admin auth but not the operator check
        var response = await PostScanAsync(gate.Id,
            qr: "ABCD12345678", adminB, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateOperatorNotAssigned, body.Error!.Code);
    }

    [Fact]
    public async Task Holder_not_approved_records_HOLDER_NOT_APPROVED_denial()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        // visitor exists with a QR id but is still PendingApproval
        var qrId = await CreateVisitorWithQrAsync(approved: false);

        var response = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.HolderNotApproved, body.DenialReasonCode);
    }

    [Fact]
    public async Task Allowed_scan_records_an_Allowed_outcome_with_visitor_payload()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        var response = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.NotNull(body.UserProfile);
        Assert.Equal(ScanDirection.CheckIn, body.Direction);  // Both-mode cold start
    }

    [Fact]
    public async Task A_disabled_account_is_denied_even_though_its_profile_is_approved()
    {
        // Admission lives on the profile, but blocking an account and the dormant
        // sweep both write Disabled to the ACCOUNT. Until the resolver carried that
        // across, the gate read an approved profile and let a blocked holder in,
        // and DenialReasonCode.HolderDisabled was unreachable code.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        // The badge scans clean while the account is live.
        var before = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        var allowed = (await before.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, allowed.Outcome);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var userId = await appDb.UserProfiles.AsNoTracking()
                .Where(profile => profile.QrId == qrId)
                .Select(profile => profile.UserId)
                .SingleAsync();
            var user = await identityDb.Users.SingleAsync(u => u.Id == userId!.Value);
            user.AccountState = AccountState.Disabled;
            await identityDb.SaveChangesAsync();

            // The profile is deliberately left Approved: that is the state the
            // disable paths leave behind, and the point of the check.
            var admission = await appDb.UserProfiles.AsNoTracking()
                .Where(profile => profile.QrId == qrId)
                .Select(profile => profile.AdmissionState)
                .SingleAsync();
            Assert.Equal(AccountState.Approved, admission);
        }

        var after = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var denied = (await after.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, denied.Outcome);
        Assert.Equal(DenialReasonCode.HolderDisabled, denied.DenialReasonCode);
    }

    [Fact]
    public async Task Profile_type_not_in_allow_list_records_PROFILE_TYPE_NOT_ALLOWED()
    {
        var (token, _) = await CreateAdminAsync();
        var visitorPt = await CreateProfileTypeAsync("Bronze", UserType.Visitor);
        var goldPt = await CreateProfileTypeAsync("Gold", UserType.Visitor);
        var qrId = await CreateVisitorWithQrAsync(approved: true, profileTypeId: visitorPt);

        // Gate only accepts Gold
        var gate = await CreateGateAsync(token,
            allowedProfileTypeIds: new List<Guid> { goldPt },
            ownAsOperator: true, mode: DirectionMode.Both);

        var response = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.ProfileTypeNotAllowed, body.DenialReasonCode);
    }

    [Fact]
    public async Task Both_mode_infers_CheckOut_after_a_prior_CheckIn()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        // First scan: CheckIn (cold start)
        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanDirection.CheckIn, firstBody.Direction);

        // Advance past the 5-second duplicate window so the next scan is recorded
        // as a fresh row rather than absorbed.
        _factory.Time.Advance(TimeSpan.FromSeconds(6));

        // Second scan: engine infers CheckOut from the prior allowed CheckIn.
        var second = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, secondBody.Outcome);
        Assert.Equal(ScanDirection.CheckOut, secondBody.Direction);
    }

    [Fact]
    public async Task Five_second_duplicate_window_returns_the_prior_scan_id()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        // Same visitor, same gate, within 5 seconds — engine absorbs the
        // duplicate and returns the prior scan id (no second row inserted).
        var second = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(firstBody.ScanId, secondBody.ScanId);
    }

    [Fact]
    public async Task Idempotency_key_replays_the_original_response()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        var second = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // X-Idempotent-Replay header set on the second response per
        // SIMF-API-GATES-001 §9.
        Assert.Contains("X-Idempotent-Replay", second.Headers.Select(h => h.Key));
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(firstBody.ScanId, secondBody.ScanId);
    }

    [Fact]
    public async Task Idempotency_key_with_different_payload_is_409_IDEMPOTENCY_KEY_CONFLICT()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrA = await CreateVisitorWithQrAsync(approved: true);
        var qrB = await CreateVisitorWithQrAsync(approved: true);
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrA, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostScanAsync(gate.Id, qr: qrB, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.IdempotencyKeyConflict, body.Error!.Code);
    }

    [Fact]
    public async Task Returning_badge_re_scan_after_24h_window_refreshes_idempotency_without_500()
    {
        // A4 (D-592) — the returning-badge 500. A prior scan's idempotency row
        // older than the 24h replay window is filtered out by TryReplayAsync, so
        // the re-scan flows to RecordAllowed — whose insert must upsert the stale
        // row in place, not blind-Add and collide on the composite PK (Key, GateId).
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);
        var key = Guid.NewGuid().ToString();

        // Seed the returning badge's stale idempotency row (older than 24h).
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.ScanIdempotencies.Add(new ScanIdempotency
            {
                Key = key,
                GateId = gate.Id,
                RequestHash = "stale-request-hash",
                ResponseHash = "stale-response-hash",
                ScanId = null,
                StoredAt = _factory.Time.SimfNow() - TimeSpan.FromHours(25),
            });
            await appDb.SaveChangesAsync();
        }

        // The re-scan must succeed, not 500 on the PK collision.
        var response = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var rows = appDb.ScanIdempotencies
                .Where(r => r.Key == key && r.GateId == gate.Id)
                .ToList();
            // One row, refreshed in place (not duplicated, not left stale).
            Assert.Single(rows);
            Assert.NotEqual("stale-request-hash", rows[0].RequestHash);
        }
    }

    [Fact]
    public async Task Idempotency_key_reused_past_24h_replays_the_prior_scan_instead_of_500()
    {
        // #15 — a real prior scan carries the key on the append-only GateScan unique
        // index (UX_GateScan_Idempotency). Reusing that key past the 24h replay window
        // filters the idempotency row out of TryReplayAsync, so the re-scan flows to
        // RecordAllowed and its fresh GateScan insert collides with the retained prior
        // row. The idempotency contract is a replay, not a 500: the prior scan comes
        // back with X-Idempotent-Replay and no duplicate row is written.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        // Past the 24h replay-retention window: the stale idempotency row no longer
        // replays, but the GateScan unique index still retains the prior row.
        _factory.Time.Advance(TimeSpan.FromHours(25));

        var second = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("X-Idempotent-Replay", second.Headers.Select(h => h.Key));
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        // Replayed the prior scan — not a fresh insert.
        Assert.Equal(firstBody.ScanId, secondBody.ScanId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(1, await db.GateScans.CountAsync(s => s.IdempotencyKey == key));
    }

    [Fact]
    public async Task Both_mode_gate_honours_the_operator_requested_direction()
    {
        // D-509 — on a Both-mode gate the operator's دخول/خروج choice is honoured
        // even on cold start (where the alternation inference would say CheckIn).
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        var response = await PostScanAsync(gate.Id, qr: qrId, token,
            idempotencyKey: null, requestedDirection: ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, body.Outcome);
        Assert.Equal(ScanDirection.CheckOut, body.Direction);
    }

    [Fact]
    public async Task Both_mode_deliberate_direction_switch_is_not_absorbed()
    {
        // D-509 — a same-badge re-scan within the 5s window but with the OTHER
        // direction is an intentional movement, so it records a NEW scan in the
        // chosen direction (the duplicate window must not collapse it to the
        // prior direction). A same-direction re-scan is still absorbed.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        var first = await PostScanAsync(gate.Id, qr: qrId, token,
            idempotencyKey: null, requestedDirection: ScanDirection.CheckIn);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanDirection.CheckIn, firstBody.Direction);

        var second = await PostScanAsync(gate.Id, qr: qrId, token,
            idempotencyKey: null, requestedDirection: ScanDirection.CheckOut);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanDirection.CheckOut, secondBody.Direction);
        // A new row, not the absorbed prior scan.
        Assert.NotEqual(firstBody.ScanId, secondBody.ScanId);
    }

    [Fact]
    public async Task Fixed_in_gate_ignores_the_operator_requested_direction()
    {
        // D-509 — a fixed In gate always records CheckIn, even if the operator
        // (wrongly) asks for CheckOut.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.In);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        var response = await PostScanAsync(gate.Id, qr: qrId, token,
            idempotencyKey: null, requestedDirection: ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanDirection.CheckIn, body.Direction);
    }

    [Fact]
    public async Task Operator_can_list_their_own_assignments()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        var response = await GetAuthAsync("/api/v1/app/gates/my-assignments", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<OperatorGateAssignment>>>())!.Data!;
        Assert.Contains(body, a => a.GateId == gate.Id);
    }

    [Fact]
    public async Task My_daily_report_returns_the_operator_totals()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: null);

        var response = await GetAuthAsync("/api/v1/app/gates/my-reports/today", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content
            .ReadFromJsonAsync<ApiResult<OperatorDailyReport>>())!.Data!;
        Assert.True(report.Totals.Allowed >= 1);
    }

    [Fact]
    public async Task My_daily_report_aggregates_the_full_day_past_the_500_row_cap()
    {
        // A8 — Totals + DenialBreakdown must cover the FULL day, not the Take(500)
        // display grid. Seed 520 Allowed + 90 Denied (two reason codes) for one
        // operator+gate today, then assert the aggregates are full-day-correct while
        // the Rows grid stays capped at 500. Fails on the old count-the-capped-list
        // code (it would report Allowed <= 500 and drop the tail of every bucket).
        var (token, email) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var operatorId = (await users.FindByEmailAsync(email))!.Id;
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            // Seed at the service's clock so the scans fall inside today's window.
            var now = _factory.Time.SimfNow();

            GateScan Scan(ScanOutcome outcome, DenialReasonCode? reason) => new()
            {
                GateId = gate.Id,
                ScannedByUserId = operatorId,
                ScannedAt = now,
                Outcome = outcome,
                Direction = ScanDirection.CheckIn,
                DenialReasonCode = reason,
                QrIdAtScan = "seed",
                Source = ScanSource.Simulator,
            };

            var scans = new List<GateScan>();
            for (var i = 0; i < 520; i++) { scans.Add(Scan(ScanOutcome.Allowed, null)); }
            for (var i = 0; i < 60; i++)
            { scans.Add(Scan(ScanOutcome.Denied, DenialReasonCode.HolderNotApproved)); }
            for (var i = 0; i < 30; i++)
            { scans.Add(Scan(ScanOutcome.Denied, DenialReasonCode.OutsideTimeWindow)); }
            db.GateScans.AddRange(scans);
            await db.SaveChangesAsync();
        }

        var response = await GetAuthAsync("/api/v1/app/gates/my-reports/today", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content
            .ReadFromJsonAsync<ApiResult<OperatorDailyReport>>())!.Data!;

        Assert.Equal(520, report.Totals.Allowed);                    // full-day COUNT
        Assert.Equal(90, report.Totals.Denied);
        Assert.Equal(90, report.DenialBreakdown.Sum(b => b.Count));  // full-day GROUP BY
        Assert.Equal(500, report.Rows.Count);                        // grid still capped
    }

    [Fact]
    public async Task Inactive_gate_records_a_GATE_INACTIVE_AT_SCAN_denial_at_200()
    {
        // DEF-STF-008 — GateScanResultKind.GateInactive (HTTP 503 GATE_INACTIVE)
        // was dead: NOTHING ever returned it. An inactive gate is denied by
        // engine step 5 as a RECORDED denial at HTTP 200, which keeps the
        // append-only GateScan audit row for the attempt and hands the operator
        // the localised "This gate is currently inactive." The 503 arm has been
        // removed; this test pins the behaviour that replaced it.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, allowedProfileTypeIds: null,
            ownAsOperator: true, mode: DirectionMode.Both);
        var qrId = await CreateVisitorWithQrAsync(approved: true);

        // Deactivating keeps the operator's assignment and invalidates the
        // config-cache snapshot, so the next scan sees IsActive = false.
        var deactivate = await DeleteAuthAsync(
            $"/api/v1/admin/gates/{gate.Id}", token);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var response = await PostScanAsync(gate.Id, qr: qrId, token,
            idempotencyKey: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, body.Outcome);
        Assert.Equal(DenialReasonCode.GateInactiveAtScan, body.DenialReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(body.DenialMessage));

        // The attempt is auditable: the denial is persisted, not discarded with
        // an envelope failure the way a 503 would have been.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var recorded = await db.GateScans.AsNoTracking()
            .Where(s => s.GateId == gate.Id
                     && s.DenialReasonCode == DenialReasonCode.GateInactiveAtScan)
            .CountAsync();
        Assert.Equal(1, recorded);
    }

    // -- Helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string token, string? idempotencyKey,
        ScanDirection? requestedDirection = null)
    {
        // The wire DTO is the endpoint's PostScanRequest (field "direction"),
        // not the service-layer GateScanRequest ("requestedDirection") — post the
        // shape the endpoint actually binds.
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new
            {
                qr,
                idempotencyKey,
                source = ScanSource.Simulator,
                direction = requestedDirection,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<AdminGateDetail> CreateGateAsync(
        string adminToken, List<Guid>? allowedProfileTypeIds,
        bool ownAsOperator, DirectionMode mode)
    {
        var code = $"GS-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        var operatorUserIds = new List<Guid>();
        if (ownAsOperator)
        {
            var actorId = await GetCurrentAdminUserIdAsync(adminToken);
            operatorUserIds.Add(actorId);
        }

        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Scan Test Gate",
                NameArabic = "بوابة اختبار",
                DirectionMode = mode,
                AllowedProfileTypeIds = allowedProfileTypeIds ?? new List<Guid>(),
                AssignedOperatorUserIds = operatorUserIds,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<Guid> GetCurrentAdminUserIdAsync(string token)
    {
        // The JWT contains a 'sub' claim with the user id; decode without
        // verifying (test-only — verifying would re-traverse the same path
        // the API does).
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
        return Guid.Parse(jwt.Claims.First(c => c.Type == "sub" || c.Type == "nameid").Value);
    }

    private async Task<string> CreateVisitorWithQrAsync(
        bool approved, Guid? profileTypeId = null)
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test Visitor",
            AccountState = approved ? AccountState.Approved : AccountState.PendingApproval,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            ProfileTypeId = profileTypeId,
            NameArabic = "زائر اختبار",
            Name = "Test Visitor",
            NationalityId = 682, // ISO 3166-1 numeric — SA
            PlaceOfBirth = "Riyadh",
            // The gate reads admission off the PROFILE, so `approved` has to drive
            // this and not only the account above. Left at its PendingApproval
            // default, every holder is refused HolderNotApproved before the gate
            // ever reaches the allow-list or direction checks the test is about.
            AdmissionState = approved ? AccountState.Approved : AccountState.PendingApproval,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private async Task<Guid> CreateProfileTypeAsync(string name, UserType userType)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var id = Guid.NewGuid();
        appDb.ProfileTypes.Add(new SIMF.Domain.Profiles.UserProfileType
        {
            Id = id,
            Name = name,
            NameArabic = name,
            PageColor = "#244A77",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return id;
    }

    private async Task<(string Token, string Email)> CreateAdminAsync()
    {
        var email = $"gate-scan-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Scan Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return (await AuthFlow.SignInControlPanelAsync(_client, _factory, email), email);
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
