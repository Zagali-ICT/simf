using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

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
            "/api/v1/admin/users",
            new AdminCreateUserRequest
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
        Assert.Equal(AccountState.Approved, created.AccountState);
        Assert.False(await users.IsInRoleAsync(created, AdministratorRole));

        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var code = await db.AccountCodes.SingleAsync(
            c => c.UserId == created.Id
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
            "/api/v1/admin/users",
            new AdminCreateUserRequest
            {
                Email = newEmail,
                DisplayName = "Second Admin",
                GrantAdministratorRole = true,
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
            "/api/v1/admin/users",
            new AdminCreateUserRequest
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
            "/api/v1/admin/users",
            new AdminCreateUserRequest { Email = email, DisplayName = "First time" },
            adminToken);
        var response = await PostAuthAsync(
            "/api/v1/admin/users",
            new AdminCreateUserRequest { Email = email, DisplayName = "Second time" },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminEmailAlreadyRegistered, body.Error!.Code);
    }

    [Fact]
    public async Task ListUsers_returns_every_account_with_the_role_and_2FA_flags()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var freshEmail = $"in-list-{Guid.NewGuid():N}@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/users",
            new AdminCreateUserRequest { Email = freshEmail, DisplayName = "Listed" },
            adminToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminUserListResponse>>())!;
        Assert.Contains(body.Data!.Users, user => user.Email == freshEmail && !user.IsAdministrator);
        // The actor (an Administrator created for the test) must also appear.
        Assert.Contains(body.Data.Users, user => user.IsAdministrator);
    }

    [Fact]
    public async Task The_invite_code_lets_the_new_user_set_their_password_and_sign_in()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var newEmail = $"invite-flow-{Guid.NewGuid():N}@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/users",
            new AdminCreateUserRequest { Email = newEmail, DisplayName = "Invite Flow" },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == newEmail);
        var inviteCode = await db.AccountCodes
            .Where(c => c.UserId == user.Id
                && c.Purpose == AccountCodePurpose.PasswordReset
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Code)
            .FirstAsync();

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = newEmail,
                Code = inviteCode,
                NewPassword = "NewPassw0rd!",
                ConfirmPassword = "NewPassw0rd!",
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = newEmail, Password = "NewPassw0rd!" });
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
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
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
