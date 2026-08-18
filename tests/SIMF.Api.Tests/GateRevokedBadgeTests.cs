// Pins the HOLDER_DISABLED arm of the gate constraint engine to the paths that
// are supposed to reach it. Admission is decided on UserProfile.AdmissionState,
// not on the Identity account, so every path that takes a badge out of
// circulation has to write the PROFILE or the badge keeps opening the door.
// A security review found that arm unreachable once; nothing asserted on it, so
// nothing failed when it was. These tests are that assertion.
//
// One test per writer, plus one for the resolver's account-side safety net:
//   1. the untyped bulk delete            (AdminAccountService.Bulk)
//   2. the type-scoped bulk delete        (AdminAccountService.Bulk)
//   3. revoking a printed badge order     (RevokeBadgeBatchAsync), including the
//      member profile that has NO account at all, which is the ordinary case for
//      a bulk-printed badge and the one an Identity-only disable can never reach
//   4. a disabled account whose profile was somehow left Approved, which
//      QrResolver folds down to Disabled so a forgotten writer still fails closed
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
using SIMF.Domain.Badges;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GateRevokedBadgeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string DeleteReason = "Regression test revocation";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateRevokedBadgeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bulk_deleting_an_account_denies_its_badge_with_HOLDER_DISABLED()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var holder = await CreateApprovedVisitorAsync();

        // Proves the badge worked BEFORE the delete. Without this the test would
        // still pass if the scan had been denying for an unrelated reason all along.
        Assert.Equal(
            ScanOutcome.Allowed,
            (await ScanAsync(gate.Id, holder.QrId, token)).Outcome);

        var deleted = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { holder.UserId },
                Reason = DeleteReason,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(1, (await deleted.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!.Data!.Deleted);

        await AssertProfileAdmissionAsync(holder.UserId, AccountState.Disabled);

        var scan = await ScanAsync(gate.Id, holder.QrId, token);
        Assert.Equal(ScanOutcome.Denied, scan.Outcome);
        Assert.Equal(DenialReasonCode.HolderDisabled, scan.DenialReasonCode);
    }

    [Fact]
    public async Task Type_scoped_visitor_delete_denies_the_badge_with_HOLDER_DISABLED()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var holder = await CreateApprovedVisitorAsync();

        var deleted = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { holder.UserId },
                Reason = DeleteReason,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(1, (await deleted.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!.Data!.Deleted);

        await AssertProfileAdmissionAsync(holder.UserId, AccountState.Disabled);

        var scan = await ScanAsync(gate.Id, holder.QrId, token);
        Assert.Equal(ScanOutcome.Denied, scan.Outcome);
        Assert.Equal(DenialReasonCode.HolderDisabled, scan.DenialReasonCode);
    }

    [Fact]
    public async Task Revoking_a_badge_order_denies_every_member_including_the_accountless_one()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);

        // A printed order with the two shapes a member can take: one who went on
        // to claim an account, and one who never had one. The second is the whole
        // point. Disabling accounts alone leaves that badge admitting its holder,
        // because there is no account to disable.
        var batchId = await CreateBadgeBatchAsync();
        var withAccount = await CreateApprovedVisitorAsync(batchId);
        var withoutAccount = await CreateAccountlessBadgeAsync(batchId);

        Assert.Equal(
            ScanOutcome.Allowed,
            (await ScanAsync(gate.Id, withAccount.QrId, token)).Outcome);
        Assert.Equal(
            ScanOutcome.Allowed,
            (await ScanAsync(gate.Id, withoutAccount, token)).Outcome);

        var revoked = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/revoke",
            new AdminRevokeBadgeBatchRequest { BatchId = batchId },
            token);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(2, (await revoked.Content
            .ReadFromJsonAsync<ApiResult<AdminRevokeBadgeBatchResponse>>())!.Data!.RevokedCount);

        foreach (var qrId in new[] { withAccount.QrId, withoutAccount })
        {
            var scan = await ScanAsync(gate.Id, qrId, token);
            Assert.Equal(ScanOutcome.Denied, scan.Outcome);
            Assert.Equal(DenialReasonCode.HolderDisabled, scan.DenialReasonCode);
        }
    }

    [Fact]
    public async Task A_disabled_account_denies_the_badge_even_if_the_profile_reads_approved()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var holder = await CreateApprovedVisitorAsync();

        // Deliberately writes ONLY the Identity side, which is the shape the
        // security review reported: an account taken out of service while the
        // profile the gate reads still says Approved. QrResolver folds the account
        // state down so the scan fails closed anyway. This is the safety net, not
        // the mechanism: the real writers above move the profile, and the tests
        // above prove they do.
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = (await users.FindByIdAsync(holder.UserId.ToString()))!;
            user.AccountState = AccountState.Disabled;
            await users.UpdateAsync(user);
        }
        await AssertProfileAdmissionAsync(holder.UserId, AccountState.Approved);

        var scan = await ScanAsync(gate.Id, holder.QrId, token);
        Assert.Equal(ScanOutcome.Denied, scan.Outcome);
        Assert.Equal(DenialReasonCode.HolderDisabled, scan.DenialReasonCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task AssertProfileAdmissionAsync(Guid userId, AccountState expected)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var admission = await appDb.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.AdmissionState)
            .SingleAsync();
        Assert.Equal(expected, admission);
    }

    private async Task<GateScanResponse> ScanAsync(Guid gateId, string qr, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            // The endpoint binds its own PostScanRequest, whose direction field is
            // named "direction" and not the service layer's "requestedDirection".
            Content = JsonContent.Create(new
            {
                qr,
                idempotencyKey = (string?)null,
                source = ScanSource.Simulator,
                direction = (ScanDirection?)null,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
    }

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken)
    {
        // The JWT carries the actor id, which the gate needs as its operator: an
        // admin who is not assigned to the gate is refused before any scan runs.
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(adminToken);
        var actorId = Guid.Parse(jwt.Claims
            .First(claim => claim.Type == "sub" || claim.Type == "nameid").Value);

        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"RB-{Guid.NewGuid().ToString("N")[..8]}",
                Name = "Revoked Badge Test Gate",
                NameArabic = "بوابة اختبار الشارات الملغاة",
                DirectionMode = DirectionMode.Both,
                // No allow-list, so a scan reaches the holder checks these tests
                // are about instead of stopping at PROFILE_TYPE_NOT_ALLOWED.
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = new List<Guid> { actorId },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<Guid> CreateBadgeBatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var batch = new BadgeBatch
        {
            Id = Guid.NewGuid(),
            Name = "Revoke Regression Order",
            NameArabic = "طلب اختبار الإلغاء",
            CreatedAt = SimfClock.Now,
        };
        appDb.BadgeBatches.Add(batch);
        await appDb.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<(Guid UserId, string QrId)> CreateApprovedVisitorAsync(
        Guid? batchId = null)
    {
        var email = $"revoked-badge-{Guid.NewGuid():N}@simf.test";
        var qrId = NewQrId();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Revoked Badge Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(NewProfile(qrId, user.Id, batchId));
        await appDb.SaveChangesAsync();
        return (user.Id, qrId);
    }

    private async Task<string> CreateAccountlessBadgeAsync(Guid batchId)
    {
        var qrId = NewQrId();
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(NewProfile(qrId, userId: null, batchId));
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private static UserProfile NewProfile(string qrId, Guid? userId, Guid? batchId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QrId = qrId,
            ProfileTypeId = null,
            Name = "Revoked Badge Holder",
            NameArabic = "حامل شارة ملغاة",
            NationalityId = 682, // ISO 3166-1 numeric, Saudi Arabia
            PlaceOfBirth = "Riyadh",
            // The gate reads admission here and nowhere else, so a holder left at
            // the PendingApproval default is refused HolderNotApproved before the
            // scan ever reaches the branch these tests are about.
            AdmissionState = AccountState.Approved,
            BadgeBatchId = batchId ?? BadgeBatch.DirectRegistrationId,
            CreatedAt = SimfClock.Now,
        };

    private static string NewQrId() =>
        Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private async Task<(string Token, string Email)> CreateAdminAsync()
    {
        var email = $"revoked-badge-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Revoked Badge Admin",
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
