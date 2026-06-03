using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the admin-create-user and list-users endpoints
/// (decision D-042).
/// </summary>
public sealed class AdminCreateUserTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminCreateUserTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_administrator_can_create_a_new_user_and_an_invite_code_is_issued()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var newEmail = $"invited-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = newEmail,
                DisplayName = "Invited User",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        Assert.True(body.Success);
        Assert.Equal(newEmail, body.Data!.Email);
        Assert.Equal((int)TimeSpan.FromDays(7).TotalSeconds, body.Data.InviteExpiresInSeconds);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var created = (await users.FindByEmailAsync(newEmail))!;
        Assert.Equal("Invited User", created.DisplayName);
        // P4 — created accounts land in PendingApproval; an admin approves
        // them via /admin/staff/{id}/approve to mint the QR id and unlock CP.
        Assert.Equal(AccountState.PendingApproval, created.AccountState);
        Assert.False(await users.IsInRoleAsync(created, AdministratorRole));

        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var code = await db.AccountCodes.SingleAsync(
            c => c.CreateBy == created.Id
                && c.Purpose == AccountCodePurpose.PasswordReset
                && c.ConsumedAt == null);
        Assert.True(code.ExpiresAt > code.CreatedAt.AddDays(6));
    }

    [Fact]
    public async Task Granting_the_Administrator_role_adds_the_user_to_the_role()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var newEmail = $"second-admin-{Guid.NewGuid():N}@simf.test";

        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = newEmail,
                DisplayName = "Second Admin",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var created = (await users.FindByEmailAsync(newEmail))!;
        Assert.True(await users.IsInRoleAsync(created, AdministratorRole));
    }

    [Fact]
    public async Task A_non_administrator_caller_is_forbidden()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = $"x-{Guid.NewGuid():N}@simf.test",
                DisplayName = "Should never be created",
            },
            visitor.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_email_is_rejected_with_409()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var email = $"dup-{Guid.NewGuid():N}@simf.test";

        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest { Email = email, DisplayName = "First time" },
            adminToken);
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest { Email = email, DisplayName = "Second time" },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminEmailAlreadyRegistered, body.Error!.Code);
    }

    [Fact]
    public async Task Admins_list_returns_only_UserType_Admin_after_P7c_split()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        // P7c — every user created via /admin/admins lands with
        // UserType = Admin (with or without the Administrator RBAC role).
        // They all appear in the admins list; visitors do not.
        var anotherAdminEmail = $"in-list-{Guid.NewGuid():N}@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = anotherAdminEmail, DisplayName = "Listed",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/list",
            new GridQuery { Top = 200 },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!;
        Assert.True(body.Success);
        // The actor + the newly-created Admin both appear.
        Assert.Contains(body.Data!.Items, user => user.IsAdministrator);
        Assert.Contains(body.Data!.Items, user => user.Email == anotherAdminEmail);
    }

    [Fact]
    public async Task Visitors_list_returns_only_UserType_Visitor_after_P7c_split()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitorEmail = $"visitor-{Guid.NewGuid():N}@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest { Email = visitorEmail, DisplayName = "Visitor" },
            adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/list",
            new GridQuery { Top = 200 },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!;
        Assert.Contains(body.Data!.Items, user => user.Email == visitorEmail);
        // P7c — Admin users (UserType = Admin) must NOT appear in the
        // visitor list. The IsAdministrator flag on the summary is the
        // RBAC-role check, which is a sufficient proxy here because today
        // every Admin user is created via /admin/admins with the
        // Administrator role.
        Assert.DoesNotContain(body.Data.Items, user => user.IsAdministrator);
    }

    [Fact]
    public async Task ListVisitors_filters_by_email_substring()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var marker = $"f{Guid.NewGuid():N}".Substring(0, 8);
        var emailA = $"{marker}-a@simf.test";
        var emailB = $"{marker}-b@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest { Email = emailA, DisplayName = "Filter A" },
            adminToken);
        await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest { Email = emailB, DisplayName = "Filter B" },
            adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/list",
            new GridQuery { Filters = new Dictionary<string, string> { ["email"] = marker } },
            adminToken);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!;
        Assert.Equal(2, body.Data!.Total);
        Assert.All(body.Data.Items, user => Assert.Contains(marker, user.Email));
    }

    [Fact]
    public async Task ListUsers_paginates_and_echoes_skip_and_top()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/list",
            new GridQuery { Skip = 0, Top = 1 },
            adminToken);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!;
        Assert.Single(body.Data!.Items);
        Assert.Equal(0, body.Data.Skip);
        Assert.Equal(1, body.Data.Top);
    }

    [Fact]
    public async Task ListUsers_sorts_by_email_ascending_when_asked()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/list",
            new GridQuery { Sort = "email", SortDescending = false, Top = 200 },
            adminToken);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!;
        var emails = body.Data!.Items.Select(u => u.Email).ToList();
        var sorted = emails.OrderBy(e => e, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, emails);
    }

    [Fact]
    public async Task Creating_a_user_does_not_mint_a_QR_id_until_approval_in_P4()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var email = $"qr-{Guid.NewGuid():N}@simf.test";

        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest { Email = email, DisplayName = "QR Test" },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var created = (await users.FindByEmailAsync(email))!;
        Assert.Equal(AccountState.PendingApproval, created.AccountState);
        // D-106: QR lives on UserProfile now; minting still happens at approve-time
        // (covered by AdminApprovalTests.Approve_staff_flips_state_to_Approved_and_mints_QR_id).
        var qr = await appDb.UserProfiles
            .Where(p => p.UserId == created.Id)
            .Select(p => p.QrId)
            .SingleOrDefaultAsync();
        Assert.True(string.IsNullOrEmpty(qr));
    }

    [Fact]
    public async Task The_invite_code_lets_the_new_user_set_their_password_and_sign_in()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var newEmail = $"invite-flow-{Guid.NewGuid():N}@simf.test";
        // P2 — grant Administrator so the new user is real staff (a CP role);
        // otherwise the audience gate treats this user as a visitor and the
        // Cp-audience sign-in below is rejected.
        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = newEmail,
                DisplayName = "Invite Flow",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == newEmail);
        var inviteCode = await db.AccountCodes
            .Where(c => c.CreateBy == user.Id
                && c.Purpose == AccountCodePurpose.PasswordReset
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Code)
            .FirstAsync();

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = newEmail,
                Code = inviteCode,
                NewPassword = "NewPassw0rd!",
                ConfirmPassword = "NewPassw0rd!",
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // P4 — invited staff land in PendingApproval; approve before signing in.
        var approve = await PostAuthAsync(
            $"/api/v1/admin/admins/{user.Id}/approve",
            new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = newEmail,
                Password = "NewPassw0rd!",
                Audience = SignInAudience.Cp,   // P2 — invited user is staff (Administrator role)
            });
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
        var signBody = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        // No 2FA on this account yet, so tokens come back directly (D-033).
        Assert.NotNull(signBody.Data!.Tokens);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-create-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Administrator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,   // P7b — audience gate keys off UserType
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
                Audience = SignInAudience.Cp,   // P2 — admin helper
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }
}
