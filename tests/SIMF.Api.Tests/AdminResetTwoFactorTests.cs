using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the admin-driven 2FA reset endpoint
/// (decision D-041).
/// </summary>
public sealed class AdminResetTwoFactorTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminResetTwoFactorTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_administrator_can_reset_another_users_2FA()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var (targetEmail, targetUserId) = await EnrolTwoFactorVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = targetEmail,
                Reason = "User reported lost phone on a call from a known number.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var target = (await users.FindByIdAsync(targetUserId.ToString()))!;
        Assert.False(await users.GetTwoFactorEnabledAsync(target));
        Assert.Null(await users.GetAuthenticatorKeyAsync(target));

        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var remaining = await db.TotpRecoveryCodes.CountAsync(c => c.UserId == targetUserId);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task A_non_administrator_caller_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = "someone@example.com",
                Reason = "Lorem ipsum dolor sit amet.",
            },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_cannot_reset_their_own_2FA()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        // The admin's own email — pulled out of the token below by decoding.
        var adminEmail = ExtractEmailFromAccessToken(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = adminEmail,
                Reason = "Trying to reset my own account, this should fail.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminCannotResetSelf, body.Error!.Code);
    }

    [Fact]
    public async Task An_administrator_cannot_reset_another_administrators_2FA()
    {
        var firstAdminToken = await CreateAdministratorAndSignInAsync();
        var (secondAdminEmail, _) = await CreateAdministratorAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = secondAdminEmail,
                Reason = "Should not be allowed — admin-vs-admin.",
            },
            firstAdminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminCannotResetAdministrator, body.Error!.Code);
    }

    [Fact]
    public async Task Resetting_a_user_writes_an_audit_row_with_actor_and_subject()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var (targetEmail, targetUserId) = await EnrolTwoFactorVisitorAsync();

        await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = targetEmail,
                Reason = "User reported lost phone, identity verified in person.",
            },
            adminToken);

        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await app.OperationLog
            .Where(entry => entry.EventType == "Admin.TwoFactorReset"
                && entry.SubjectUserId == targetUserId)
            .OrderByDescending(entry => entry.TimestampUtc)
            .FirstAsync();
        Assert.Equal(targetEmail, row.SubjectEmail);
        Assert.NotNull(row.ActorUserId);
        Assert.NotEqual(targetUserId, row.ActorUserId);
        Assert.Contains("identity verified", row.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_target_email_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/users/reset-two-factor",
            new AdminResetTwoFactorRequest
            {
                Email = $"missing-{Guid.NewGuid():N}@example.com",
                Reason = "Should be 404 — no such user.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthAccountNotFound, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var (email, _) = await CreateAdministratorAsync();
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var challenge =
            (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;

        // The admin in this helper has TwoFactorEnabled=true but no
        // authenticator key paired (we created the user via UserManager
        // directly, no enrolment). Sign-in returns an Mfa ticket; the
        // selector at SignInService (D-040) routes to TOTP only when an
        // authenticator key exists, so this admin actually gets the email-OTP
        // path. Force-enable TwoFactor via the existing helper isn't enough;
        // we set up a real TOTP secret instead.
        return challenge.Tokens?.AccessToken
            ?? throw new InvalidOperationException(
                "Test admin should sign in without 2FA — make sure TwoFactorEnabled is off.");
    }

    private async Task<(string Email, Guid UserId)> CreateAdministratorAsync()
    {
        var email = $"admin-reset-{Guid.NewGuid():N}@simf.test";

        using var scope = _factory.Services.CreateScope();
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
        return (email, user.Id);
    }

    private async Task<(string Email, Guid UserId)> EnrolTwoFactorVisitorAsync()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        // Setup + confirm — sets the authenticator key, flips TwoFactorEnabled
        // and mints recovery codes, all of which the admin reset should wipe.
        var setup = await PostAuthAsync<object>(
            "/api/v1/auth/totp/setup", null, tokens.AccessToken);
        var secret = (await setup.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!
            .Data!.Secret;
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);
        await PostAuthAsync(
            "/api/v1/auth/totp/confirm",
            new TotpConfirmRequest { Code = code },
            tokens.AccessToken);

        return (tokens.User.Email, tokens.User.Id);
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

    private static string ExtractEmailFromAccessToken(string accessToken)
    {
        // JWTs are three base64url-encoded segments. Decode the payload.
        var parts = accessToken.Split('.');
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("email").GetString()!;
    }
}
