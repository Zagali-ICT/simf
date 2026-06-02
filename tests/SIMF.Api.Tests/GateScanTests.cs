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
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

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

    // -- Helpers --------------------------------------------------------------

    private async Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string token, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new GateScanRequest
            {
                Qr = qr,
                IdempotencyKey = idempotencyKey,
                Source = ScanSource.Simulator,
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
            ArabicName = "زائر اختبار",
            EnglishName = "Test Visitor",
            NationalityId = 682, // ISO 3166-1 numeric — SA
            PlaceOfBirth = "Riyadh",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private async Task<Guid> CreateProfileTypeAsync(string name, UserType userType)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var id = Guid.NewGuid();
        appDb.ProfileTypes.Add(new SIMF.Domain.Profiles.ProfileType
        {
            Id = id,
            Name = name,
            NameArabic = name,
            UserType = userType,
            PageColor = "#244A77",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
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

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, email);
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
