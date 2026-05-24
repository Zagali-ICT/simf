// Tests: SIMF.Api.Tests/AdminApprovalTests.cs (this file).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the visitor / staff approval workflow (P4).
/// Pairs with <c>AdminCreateUserTests</c> — accounts created via
/// <c>/admin/staff</c> or <c>/admin/visitors</c> now land in PendingApproval
/// and must be approved before they can sign in to the CP / mint a QR id.
/// </summary>
public sealed class AdminApprovalTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

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
        var subject = await db.Users.SingleAsync(u => u.Email == subjectEmail);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);
        Assert.Null(subject.QrId);
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
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, subject.AccountState);
        Assert.False(string.IsNullOrEmpty(subject.QrId));
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
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Rejected, subject.AccountState);
        Assert.Equal(reason, subject.RejectionReason);
        // EN-only admin input mirrors to the Arabic field (R1 default).
        Assert.Equal(reason, subject.RejectionReasonArabic);
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
            var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
            subject.AccountState = AccountState.PendingApproval;
            await db.SaveChangesAsync();
        }

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var reconsidered = await verifyDb.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, reconsidered.AccountState);
        Assert.Null(reconsidered.RejectionReason);
        Assert.Null(reconsidered.RejectionReasonArabic);
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
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, subject.AccountState);
        Assert.False(string.IsNullOrEmpty(subject.QrId));
        Assert.True(AuditEntryExists(subject.Email!, AuditEvents.AdminVisitorApproved));
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
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
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
        var sign = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
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
}
