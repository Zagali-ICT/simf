using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for sign-in and the two second factors (SIMF-API-001
/// section 12.4).
/// </summary>
public sealed class SignInTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"signin-{Guid.NewGuid():N}@simf.test";

    [Fact]
    public async Task SignIn_with_an_unknown_email_returns_401()
    {
        var response = await SignInAsync(NewEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, body!.Error!.Code);
    }

    [Fact]
    public async Task SignIn_with_a_wrong_password_returns_401()
    {
        var email = await RegisterVerifiedVisitorAsync();

        var response = await SignInAsync(email, "Wrong1!Password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_before_email_verification_returns_403()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await SignInAsync(email, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthEmailNotVerified, body!.Error!.Code);
    }

    [Fact]
    public async Task SignIn_for_a_disabled_account_returns_403()
    {
        var email = await RegisterVerifiedVisitorAsync();
        SetAccountState(email, AccountState.Disabled);

        var response = await SignInAsync(email, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthAccountDisabled, body!.Error!.Code);
    }

    [Fact]
    public async Task SignIn_locks_the_account_after_five_wrong_passwords()
    {
        var email = await RegisterVerifiedVisitorAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bad = await SignInAsync(email, "Wrong1!Password");
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var locked = await SignInAsync(email, "Wrong1!Password");
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);

        // The correct password is refused too while the account is locked.
        var correctButLocked = await SignInAsync(email, Password);
        Assert.Equal(HttpStatusCode.Locked, correctButLocked.StatusCode);
    }

    [Fact]
    public async Task A_visitor_signs_in_and_completes_with_the_emailed_code()
    {
        var email = await RegisterVerifiedVisitorAsync();

        var challenge = await ExpectChallengeAsync(email, Password);
        Assert.True(challenge.MfaRequired);
        Assert.NotNull(challenge.OtpToken);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = GetActiveCode(email, AccountCodePurpose.SignInOtp),
            });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(email, tokens.User.Email);
    }

    [Fact]
    public async Task Verify_otp_with_a_wrong_code_returns_400()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);
        var realCode = GetActiveCode(email, AccountCodePurpose.SignInOtp);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = realCode == "000000" ? "999999" : "000000",
            });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthOtpInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Verify_otp_rejects_the_ticket_after_five_wrong_codes()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);
        var wrong = new VerifyOtpRequest { OtpToken = challenge.OtpToken!, Code = "000001" };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/verify-otp", wrong);
        }

        var capped = await _client.PostAsJsonAsync("/api/v1/auth/verify-otp", wrong);
        Assert.Equal(HttpStatusCode.BadRequest, capped.StatusCode);
        var body = await capped.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthOtpTokenInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task An_otp_token_used_at_verify_totp_is_rejected()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.OtpToken!, Code = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthMfaTokenInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task An_administrator_signs_in_and_completes_with_a_TOTP_code()
    {
        var (adminEmail, secret) = await CreateAdminAsync();

        var challenge = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);
        Assert.NotNull(challenge.MfaToken);

        var totp = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.MfaToken!, Code = totp });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }

    [Fact]
    public async Task Verify_totp_with_a_wrong_code_returns_400()
    {
        var (adminEmail, _) = await CreateAdminAsync();
        var challenge = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.MfaToken!, Code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthTotpInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task A_TOTP_code_cannot_be_used_twice()
    {
        var (adminEmail, secret) = await CreateAdminAsync();
        var totp = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var first = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);
        var firstVerify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = first.MfaToken!, Code = totp });
        Assert.Equal(HttpStatusCode.OK, firstVerify.StatusCode);

        var second = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);
        var replay = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = second.MfaToken!, Code = totp });

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Verify_totp_after_the_ticket_expires_returns_400()
    {
        var (adminEmail, _) = await CreateAdminAsync();
        var challenge = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);

        _factory.Time.Advance(TimeSpan.FromMinutes(6));

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.MfaToken!, Code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthMfaTokenExpired, body!.Error!.Code);
    }

    [Fact]
    public async Task A_completed_sign_in_writes_a_SignInSucceeded_audit_entry()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = GetActiveCode(email, AccountCodePurpose.SignInOtp),
            });

        Assert.True(AuditEntryExists(email, AuditEvents.SignInSucceeded));
    }

    [Fact]
    public async Task Bad_credentials_write_a_SignInBadCredentials_audit_entry()
    {
        var email = await RegisterVerifiedVisitorAsync();

        await SignInAsync(email, "Wrong1!Password");

        Assert.True(AuditEntryExists(email, AuditEvents.SignInBadCredentials));
    }

    // -- #34 — second factor is conditional on TwoFactorEnabled ----------------

    [Fact]
    public async Task SignIn_returns_tokens_directly_when_TwoFactorEnabled_is_false_for_a_visitor()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        // TwoFactorEnabled stays false (Identity default) — the API short-circuits.

        var response = await SignInAsync(email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success);
        Assert.False(body.Data!.MfaRequired);
        Assert.Null(body.Data.MfaToken);
        Assert.Null(body.Data.OtpToken);
        Assert.NotNull(body.Data.Tokens);
        Assert.NotEmpty(body.Data.Tokens!.AccessToken);
        Assert.NotEmpty(body.Data.Tokens.RefreshToken);
    }

    [Fact]
    public async Task SignIn_returns_tokens_directly_when_TwoFactorEnabled_is_false_for_a_cp_user()
    {
        var (email, _) = await CreateAdminAsync();
        DisableTwoFactor(email);

        var response = await SignInAsync(email, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.False(body.Data!.MfaRequired);
        Assert.NotNull(body.Data.Tokens);
        Assert.NotEmpty(body.Data.Tokens!.AccessToken);
    }

    [Fact]
    public async Task SignIn_with_TwoFactor_off_for_a_visitor_does_not_issue_a_sign_in_otp()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });

        await SignInAsync(email, Password);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(u => u.Email == email);
        var otpCount = database.AccountCodes
            .Count(c => c.UserId == user.Id && c.Purpose == AccountCodePurpose.SignInOtp);
        Assert.Equal(0, otpCount);
    }

    // -- P2 — audience gate ---------------------------------------------------

    [Fact]
    public async Task CP_audience_rejects_a_visitor_with_AUTH_WRONG_SURFACE_CP()
    {
        var email = await RegisterVerifiedVisitorAsync();

        var response = await SignInAsync(email, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthWrongSurfaceCp, body!.Error!.Code);
        Assert.True(AuditEntryExists(email, AuditEvents.SignInWrongSurface));
    }

    [Fact]
    public async Task Web_audience_rejects_a_user_with_a_CP_role_with_AUTH_WRONG_SURFACE_WEB()
    {
        var (adminEmail, _) = await CreateAdminAsync();

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Web);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthWrongSurfaceWeb, body!.Error!.Code);
        Assert.True(AuditEntryExists(adminEmail, AuditEvents.SignInWrongSurface));
    }

    [Fact]
    public async Task App_audience_uses_the_same_visitor_rule_as_Web()
    {
        var (adminEmail, _) = await CreateAdminAsync();
        var visitorEmail = await RegisterVerifiedVisitorAsync();

        // Staff via App → rejected (App is visitor-only, same rule as Web).
        var adminViaApp = await SignInAsync(adminEmail, Password, SignInAudience.App);
        Assert.Equal(HttpStatusCode.Forbidden, adminViaApp.StatusCode);
        var adminBody = await adminViaApp.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthWrongSurfaceWeb, adminBody!.Error!.Code);

        // Visitor via App → accepted (gets the email-OTP challenge — TwoFactorEnabled
        // is on because RegisterVerifiedVisitorAsync turns it on for the OTP path).
        var visitorViaApp = await SignInAsync(visitorEmail, Password, SignInAudience.App);
        Assert.Equal(HttpStatusCode.OK, visitorViaApp.StatusCode);
        var visitorBody =
            (await visitorViaApp.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(visitorBody.Data!.MfaRequired);
        Assert.NotNull(visitorBody.Data.OtpToken);
    }

    private void DisableTwoFactor(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.TwoFactorEnabled = false;
        database.SaveChanges();
    }

    // -- helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        SignInAsync(email, password, SignInAudience.Web);

    /// <summary>P2 — variant that lets a test set the audience explicitly.</summary>
    private Task<HttpResponseMessage> SignInAsync(
        string email, string password, SignInAudience audience) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = password, Audience = audience });

    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });

    private async Task<SignInResponse> ExpectChallengeAsync(string email, string password)
    {
        var response = await SignInAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
    }

    /// <summary>P2 — variant that lets a test set the audience explicitly.</summary>
    private async Task<SignInResponse> ExpectChallengeAsync(
        string email, string password, SignInAudience audience)
    {
        var response = await SignInAsync(email, password, audience);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
    }

    private async Task<string> RegisterVerifiedVisitorAsync()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        // The OTP-path tests in this file expect the sign-in second factor to
        // run; D-033 makes that conditional on TwoFactorEnabled, so opt in.
        EnableTwoFactor(email);
        return email;
    }

    private void EnableTwoFactor(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.TwoFactorEnabled = true;
        database.SaveChanges();
    }

    /// <summary>
    /// Creates a fresh Control Panel account with its own authenticator secret.
    /// Each TOTP test needs its own account — the replay guard is per-account.
    /// </summary>
    private async Task<(string Email, string TotpSecret)> CreateAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@simf.test";
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        if (!await roleManager.RoleExistsAsync("Administrator"))
        {
            await roleManager.CreateAsync(new SimfRole { Name = "Administrator" });
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test Administrator",
            AccountState = AccountState.Approved,
            // The TOTP-path tests expect the sign-in second factor to run; D-033
            // makes that conditional on TwoFactorEnabled, so opt in.
            TwoFactorEnabled = true,
        };
        await userManager.CreateAsync(user, Password);
        // ASP.NET Core Identity's internal token coordinates for the TOTP key.
        await userManager.SetAuthenticationTokenAsync(
            user, "[AspNetUserStore]", "AuthenticatorKey", secret);
        await userManager.AddToRoleAsync(user, "Administrator");
        return (email, secret);
    }

    private string GetActiveCode(string email, AccountCodePurpose purpose)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
    }

    private void SetAccountState(string email, AccountState state)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.AccountState = state;
        database.SaveChanges();
    }

    private bool AuditEntryExists(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog.Any(
            entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }
}
