// Regression cover for the idempotent-replay recovery in
// SIMF.Infrastructure/AccessControl/GateOperatorService.cs.
//
// The idempotency row is committed with ScanId = null and the real id is
// back-filled by a SECOND statement issued after that commit. When the back-fill
// never lands -- the scanner's HTTP request times out just after the commit and the
// client disconnect cancels the token, the statement fails transiently, or the
// process restarts between the two -- the committed idempotency row keeps
// ScanId = null for the whole 24h retention window. The scanner then does exactly
// what a timeout tells it to do and retries with the same Idempotency-Key.
//
// The replay must answer with the scan that actually committed. Answering with a
// blank denial (ScanId 0, outcome Denied, no reason code, no visitor) shows the
// operator a denial card for an approved attendee whose GateScan row says Allowed,
// and repeats it for every retry with that key.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Gates;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GateScanIdempotencyRecoveryTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateScanIdempotencyRecoveryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Replay_of_a_lost_back_fill_returns_the_allowed_scan_not_a_blank_denial()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, ownAsOperator: true);
        var qrId = await CreateApprovedVisitorWithQrAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, firstBody.Outcome);
        Assert.True(firstBody.ScanId > 0);

        // The scan is committed. Null the idempotency pointer to stand in for the
        // back-fill that never ran -- the exact state a cancelled token, a failed
        // second statement or a restart between the two leaves behind.
        await NullOutIdempotencyPointerAsync(key, gate.Id);

        var retry = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("X-Idempotent-Replay", retry.Headers.Select(h => h.Key));
        var retryBody = (await retry.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        // The committed scan, recovered from the GateScan row's own
        // (IdempotencyKey, GateId) unique index -- not EmptyResponse.
        Assert.Equal(firstBody.ScanId, retryBody.ScanId);
        Assert.Equal(ScanOutcome.Allowed, retryBody.Outcome);
        Assert.Null(retryBody.DenialReasonCode);
        Assert.Null(retryBody.DenialMessage);
        Assert.NotNull(retryBody.UserProfile);
        Assert.Equal(firstBody.UserProfile!.Id, retryBody.UserProfile!.Id);

        // A replay, not a second admission: the append-only log still holds one row.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(1, await db.GateScans.CountAsync(s => s.IdempotencyKey == key));
    }

    [Fact]
    public async Task Every_retry_of_a_lost_back_fill_replays_the_same_allowed_scan()
    {
        // The pointer stays null (the recovery is a read, not a self-heal), so the
        // SECOND retry has to take the same recovery path as the first. Without it
        // the operator would keep turning the attendee away for the full 24h window,
        // which is what makes this worse than a single mis-shown card.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, ownAsOperator: true);
        var qrId = await CreateApprovedVisitorWithQrAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        await NullOutIdempotencyPointerAsync(key, gate.Id);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var retry = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var retryBody = (await retry.Content
                .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
            Assert.Equal(ScanOutcome.Allowed, retryBody.Outcome);
            Assert.Equal(firstBody.ScanId, retryBody.ScanId);
        }
    }

    [Fact]
    public async Task Replay_of_a_lost_back_fill_preserves_a_recorded_denial()
    {
        // The same recovery has to carry a DENIAL back faithfully too. A denial that
        // replayed as EmptyResponse would drop the reason code and the localised
        // message the operator reads, leaving an unexplained refusal on screen.
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token, ownAsOperator: true);
        var qrId = await CreateUnapprovedVisitorWithQrAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Denied, firstBody.Outcome);
        Assert.Equal(DenialReasonCode.HolderNotApproved, firstBody.DenialReasonCode);

        await NullOutIdempotencyPointerAsync(key, gate.Id);

        var retry = await PostScanAsync(gate.Id, qr: qrId, token, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryBody = (await retry.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;

        Assert.Equal(firstBody.ScanId, retryBody.ScanId);
        Assert.Equal(ScanOutcome.Denied, retryBody.Outcome);
        Assert.Equal(DenialReasonCode.HolderNotApproved, retryBody.DenialReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(retryBody.DenialMessage));
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Puts the committed idempotency row back into the state a lost
    /// back-fill leaves it in: the row is there, the request hash matches, and the
    /// pointer to the scan is null.</summary>
    private async Task NullOutIdempotencyPointerAsync(string key, Guid gateId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.ScanIdempotencies
            .SingleAsync(r => r.Key == key && r.GateId == gateId);
        // Guard the premise: if the back-fill ever stopped running at all, these
        // tests would pass for the wrong reason.
        Assert.True(
            row.ScanId.HasValue,
            "The idempotency row was already unpointed, so nulling it proves nothing.");
        row.ScanId = null;
        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string token, string? idempotencyKey)
    {
        // The wire DTO is the endpoint's PostScanRequest (field "direction"), not
        // the service-layer GateScanRequest ("requestedDirection").
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new
            {
                qr,
                idempotencyKey,
                source = ScanSource.Simulator,
                direction = (ScanDirection?)null,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken, bool ownAsOperator)
    {
        var operatorUserIds = new List<Guid>();
        if (ownAsOperator)
        {
            operatorUserIds.Add(CurrentAdminUserId(adminToken));
        }

        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"GIR-{Guid.NewGuid().ToString("N")[..8]}",
                Name = "Idempotency Recovery Gate",
                NameArabic = "بوابة اختبار",
                DirectionMode = DirectionMode.Both,
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = operatorUserIds,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    /// <summary>Reads the user id out of the issued JWT's subject claim. Decoded
    /// without verifying: verifying here would only re-walk the path the API just
    /// walked to issue it.</summary>
    private static Guid CurrentAdminUserId(string token)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
        return Guid.Parse(jwt.Claims.First(c => c.Type == "sub" || c.Type == "nameid").Value);
    }

    private Task<string> CreateApprovedVisitorWithQrAsync() =>
        CreateVisitorWithQrAsync(AccountState.Approved);

    private Task<string> CreateUnapprovedVisitorWithQrAsync() =>
        CreateVisitorWithQrAsync(AccountState.PendingApproval);

    private async Task<string> CreateVisitorWithQrAsync(AccountState admissionState)
    {
        var email = $"gate-replay-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Replay Test Visitor",
            AccountState = admissionState,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            NameArabic = "زائر اختبار",
            Name = "Replay Test Visitor",
            NationalityId = 682, // ISO 3166-1 numeric -- SA.
            PlaceOfBirth = "Riyadh",
            // The gate reads admission off the PROFILE, so this is the field that
            // decides allowed vs HolderNotApproved.
            AdmissionState = admissionState,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private async Task<(string Token, string Email)> CreateAdminAsync()
    {
        var email = $"gate-replay-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Replay Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return (await AuthFlow.SignInControlPanelAsync(_client, _factory, email), email);
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
