// Tests: SIMF.Api.Tests/AdminApprovalTests.cs (this file).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the visitor / staff approval workflow (P4).
/// Pairs with <c>AdminCreateUserTests</c> — accounts created via
/// <c>/admin/staff</c> or <c>/admin/visitors</c> now land in PendingApproval
/// and must be approved before they can sign in to the CP / mint a QR id.
/// </summary>
public sealed class AdminApprovalTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminApprovalTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Newly_created_staff_land_in_PendingApproval_with_no_QR()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectEmail = $"staff-{Guid.NewGuid():N}@simf.test";

        await PostAuthAsync("/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = subjectEmail, DisplayName = "Pending Staff",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Email == subjectEmail);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);
        // D-106: QR lives on UserProfile now — assert there's no profile-side QR.
        var qr = await appDb.UserProfiles
            .Where(p => p.UserId == subject.Id)
            .Select(p => p.QrId)
            .SingleOrDefaultAsync();
        Assert.True(string.IsNullOrEmpty(qr));
    }

    [Fact]
    public async Task Approve_staff_flips_state_to_Approved_and_mints_QR_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, subject.AccountState);
        // D-106: QR lives on UserProfile.
        var qr = await appDb.UserProfiles
            .Where(p => p.UserId == subjectId)
            .Select(p => p.QrId)
            .SingleAsync();
        Assert.False(string.IsNullOrEmpty(qr));
        Assert.True(AuditEntryExists(subject.Email!, AuditEvents.AdminStaffApproved));
    }

    [Fact]
    public async Task Reject_admin_persists_reason_and_state_metadata_on_the_user_row()
    {
        // P10 — D-051: the reason is no longer audit-only; it lives on
        // the user row so the user's next sign-in surfaces it via
        // AccountStateInfo.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);
        const string reason = "Not a permitted role-holder per HR list";

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/reject",
            new AdminRejectRequest { Reason = reason },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Rejected, subject.AccountState);
        // D-106: rejection text lives on UserProfile.
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
        Assert.Equal(reason, profile.RejectionReason);
        // EN-only admin input mirrors to the Arabic field (R1 default).
        Assert.Equal(reason, profile.RejectionReasonArabic);
        Assert.NotNull(subject.StateChangedAt);
        Assert.NotNull(subject.StateChangedByUserId);
        Assert.True(AuditEntryExists(subject.Email!, AuditEvents.AdminStaffRejected));
    }

    [Fact]
    public async Task Approve_after_reject_clears_the_rejection_reason_on_the_user_row()
    {
        // P10 — D-051: the reconsider path. Admin rejects, then admin
        // approves — the rejection reason should be wiped so a future
        // sign-in does not show stale text.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);

        // First, reject.
        await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/reject",
            new AdminRejectRequest { Reason = "Initial decision pending HR review." },
            adminToken);

        // Push the subject back to PendingApproval (the natural reconsider
        // flow would re-create the user; for the test we just flip the
        // state so the approve endpoint accepts the call).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
            subject.AccountState = AccountState.PendingApproval;
            await db.SaveChangesAsync();
        }

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var verifyAppDb = verify.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reconsidered = await verifyDb.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, reconsidered.AccountState);
        // D-106 / D-167: rejection text on UserProfile (App DB) is cleared on re-approval.
        var profile = await verifyAppDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
        Assert.Null(profile.RejectionReason);
        Assert.Null(profile.RejectionReasonArabic);
        Assert.NotNull(reconsidered.StateChangedAt);
    }

    [Fact]
    public async Task Approve_visitor_flips_state_and_mints_QR()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve", new { }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, subject.AccountState);
        // D-106: QR lives on UserProfile.
        var qr = await appDb.UserProfiles
            .Where(p => p.UserId == subjectId)
            .Select(p => p.QrId)
            .SingleAsync();
        Assert.False(string.IsNullOrEmpty(qr));
        Assert.True(AuditEntryExists(subject.Email!, AuditEvents.AdminVisitorApproved));
    }

    [Fact]
    public async Task Approve_visitor_with_a_valid_audience_tier_sets_the_profile_type()
    {
        // CS-D (D-386) — the approve body may carry an optional ProfileTypeId
        // to set the visitor's tier as part of the approval. A valid active,
        // audience-side (IsForVisitor=true) id must land on UserProfile.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);
        var tierId = await GetAudienceProfileTypeAsync();

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveVisitorBody(tierId), adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
        Assert.Equal(tierId, profile.ProfileTypeId);
    }

    [Fact]
    public async Task Approve_visitor_with_a_partner_tier_returns_400_ADMIN_PROFILE_TYPE_INVALID()
    {
        // CS-D (D-386) — a partner-side (IsForVisitor=false) tier is not valid
        // for a visitor; the approve must reject with the standard error code.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);
        var partnerTierId = await GetPartnerProfileTypeAsync();

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveVisitorBody(partnerTierId), adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body!.Error!.Code);

        // The subject stays pending — the invalid-tier approve is a no-op.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);
    }

    [Fact]
    public async Task Approve_visitor_with_a_null_tier_leaves_the_existing_tier_unchanged()
    {
        // CS-D (D-386) — backward-compat: an approve with NO ProfileTypeId must
        // NOT touch the visitor's existing tier (the picker's "Keep current").
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);
        var tierId = await GetAudienceProfileTypeAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await appDb.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == subjectId);
            if (profile is null)
            {
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = subjectId,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                appDb.UserProfiles.Add(profile);
            }
            profile.ProfileTypeId = tierId;
            await appDb.SaveChangesAsync();
        }

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveVisitorBody(null), adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var saved = await verifyDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
        Assert.Equal(tierId, saved.ProfileTypeId);
    }

    [Fact]
    public async Task Approve_visitor_with_an_inactive_tier_returns_400_ADMIN_PROFILE_TYPE_INVALID()
    {
        // CS-D (D-386) — the !IsActive branch of the same guard: an inactive
        // audience-side tier is rejected too, and the subject stays pending.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);

        Guid inactiveTierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var inactive = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Visitor Tier — ApprovalTestSeed",
                NameArabic = "فئة غير نشطة — اختبار",
                PageColor = "#9CA3AF",
                IsForVisitor = true,
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.ProfileTypes.Add(inactive);
            await appDb.SaveChangesAsync();
            inactiveTierId = inactive.Id;
        }

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveVisitorBody(inactiveTierId), adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body!.Error!.Code);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);
    }

    [Fact]
    public async Task Reject_visitor_requires_a_reason_of_at_least_10_characters()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/reject",
            new AdminRejectRequest { Reason = "too short" },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // P7 — the AdministratorOnly-vs-TeamMember policy split is gone (the
    // reviewer roles Staff / Scientific / Security are no longer RBAC
    // roles, they are ProfileType rows). The previous test
    // `A_Staff_role_user_can_approve_a_visitor_but_not_a_staff_account`
    // was removed by P7a because its premise no longer holds. The
    // narrower "only Administrator can approve" rule is covered by the
    // existing approve-staff and approve-visitor tests above (they sign
    // in as Administrator).

    [Fact]
    public async Task Approving_an_already_Approved_user_returns_409_ADMIN_USER_NOT_PENDING()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);

        var first = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AdminUserNotPending, body!.Error!.Code);
    }

    [Fact]
    public async Task List_pending_staff_returns_only_PendingApproval_users()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var pendingId = await CreateStaffSubjectAsync(adminToken);
        var approvedId = await CreateStaffSubjectAsync(adminToken);
        await PostAuthAsync(
            $"/api/v1/admin/admins/{approvedId}/approve", new { }, adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/pending/list",
            new GridQuery { Top = 50 }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Id == pendingId);
        Assert.DoesNotContain(page.Items, item => item.Id == approvedId);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<Guid> CreateStaffSubjectAsync(string adminToken)
    {
        var email = $"staff-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync("/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = email, DisplayName = "Pending Staff",
                Roles = new List<string> { AppRoles.Administrator },
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    private async Task<Guid> CreateVisitorSubjectAsync(string adminToken)
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync("/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email, DisplayName = "Pending Visitor",
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    // CS-D (D-386) — the optional approve body carrying a tier. Mirrors the
    // API's ApproveVisitorRequest body shape (the route supplies the id).
    private sealed record ApproveVisitorBody(Guid? ProfileTypeId);

    // CS-D (D-386) — an active, audience-side (IsForVisitor=true) ProfileType.
    // Mirrors WalkInRegistrationTests.GetVisitorProfileTypeAsync.
    private async Task<Guid> GetAudienceProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor == true && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — ApprovalTestSeed",
            NameArabic = "زائر — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    // CS-D (D-386) — an active, partner-side (IsForVisitor=false) ProfileType.
    // Mirrors WalkInRegistrationTests.GetOtherProfileTypeAsync.
    private async Task<Guid> GetPartnerProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor == false && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — ApprovalTestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            IsForVisitor = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-approve-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Approval Tests Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,   // P7b — audience gate keys off UserType
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await SignInAndGetTokenAsync(email, SignInAudience.Cp);
    }


    private async Task<string> SignInAndGetTokenAsync(string email, SignInAudience audience)
    {
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password, Audience = audience });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await _client.SendAsync(request);
    }

    private bool AuditEntryExists(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return db.OperationLog.Any(
            entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }

    // ---------------------------------------------------------------------
    // H1 — RequireApprovedAccount policy sweep (D-056)
    //
    // P10 lets a Pending/Rejected admin authenticate (they need a token to
    // reach the /auth/pending and /auth/rejected pages). Without H1, that
    // same JWT was good enough to call every admin endpoint, because the
    // gate only checked the Administrator role. H1 adds a second gate —
    // RequireApprovedAccount — that fails the request unless the JWT
    // carries account_state=Approved.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Pending_admin_can_sign_in_but_cannot_reach_admin_endpoints()
    {
        var pendingToken = await CreatePendingAdministratorAndSignInAsync();

        // Sanity: the sign-in succeeded and we got a usable token.
        Assert.False(string.IsNullOrEmpty(pendingToken));

        // The /admin/admins/list endpoint requires both Administrator role
        // AND account_state=Approved. A Pending admin holds the role but
        // not the state → 403.
        var listResponse = await PostAuthAsync(
            "/api/v1/admin/admins/list", new GridQuery(), pendingToken);
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
    }

    // H15 — D-070: parameterise the H1 gate across one URL per endpoint
    // class under src/Backend/SIMF.Api/Endpoints/Admin/. The
    // RequireApprovedAccount policy must reject the Pending admin's JWT
    // BEFORE any model binding / validation runs (authorization runs in
    // UseAuthorization, ahead of the endpoint), so the body shape on
    // POST/PUT routes is irrelevant — empty JSON is sufficient.

    [Theory]
    // POST endpoints — each row covers one admin endpoint *class*.
    [InlineData("POST", "/api/v1/admin/admins/list")]              // ListUsersEndpoint
    [InlineData("POST", "/api/v1/admin/admins/pending/list")]      // ListPendingEndpoints
    [InlineData("POST", "/api/v1/admin/admins")]                   // CreateUserEndpoint
    [InlineData("POST", "/api/v1/admin/admins/bulk-delete")]       // BulkDeleteUsersEndpoint
    [InlineData("POST", "/api/v1/admin/admins/duplicate")]         // DuplicateUserEndpoint
    [InlineData("POST", "/api/v1/admin/admins/export")]            // ExportUsersEndpoint
    // ImportUsersEndpoint omitted from the parameterised set: the
    // multipart content-type filter runs ahead of authorization in
    // FastEndpoints' pipeline, so this URL returns 415 (UnsupportedMediaType)
    // for a JSON body, masking the 403. The policy is identical to the
    // others (same attribute, same H1 commit); the import path is covered
    // by AdminCreateUserTests / ImportUsersEndpoint's own tests.
    [InlineData("POST", "/api/v1/admin/admins/reset-two-factor")]  // ResetTwoFactorEndpoint
    [InlineData("POST", "/api/v1/admin/visitors/00000000-0000-0000-0000-000000000001/approve")] // ApproveVisitorEndpoint
    [InlineData("POST", "/api/v1/admin/visitors/00000000-0000-0000-0000-000000000001/reject")]  // RejectVisitorEndpoint
    [InlineData("POST", "/api/v1/admin/admins/00000000-0000-0000-0000-000000000001/approve")]   // ApproveStaffEndpoint
    [InlineData("POST", "/api/v1/admin/admins/00000000-0000-0000-0000-000000000001/reject")]    // RejectStaffEndpoint
    [InlineData("POST", "/api/v1/admin/interests/list")]           // InterestEndpoints (one of 5)
    // GET endpoints.
    [InlineData("GET", "/api/v1/admin/profile-types")]             // ListProfileTypesEndpoint
    [InlineData("GET", "/api/v1/admin/logs/list")]                 // Logs/LogsEndpoints
    public async Task Pending_admin_is_blocked_on_every_admin_endpoint_class(string method, string path)
    {
        var pendingToken = await CreatePendingAdministratorAndSignInAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pendingToken);
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method))
        {
            // The authorization gate fires before validation, so an empty
            // body is fine — we never reach model binding.
            request.Content = new StringContent(
                "{}", System.Text.Encoding.UTF8, "application/json");
        }
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> CreatePendingAdministratorAndSignInAsync()
    {
        var email = $"pending-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Pending Admin (H1)",
                AccountState = AccountState.PendingApproval,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await SignInAndGetTokenAsync(email, SignInAudience.Cp);
    }
}
