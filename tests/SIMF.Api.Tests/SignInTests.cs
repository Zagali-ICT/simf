using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
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
/// Integration tests for sign-in and the two second factors (SIMF-API-001
/// section 12.4).
/// </summary>
public sealed class SignInTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";

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
            "/api/v1/app/auth/verify-otp",
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
    public async Task Sending_the_emailed_code_writes_no_in_app_notification()
    {
        // BUG-015 — every 2FA sign-in used to write a "Sign-in code sent" row, so
        // the notification centre filled with OTP notices and buried the
        // meaningful ones. The code travels by email; the trail is the
        // SignIn.SecondFactorIssued audit row, not a user-facing notification.
        var email = await RegisterVerifiedVisitorAsync();

        var challenge = await ExpectChallengeAsync(email, Password);
        Assert.True(challenge.MfaRequired);
        Assert.NotNull(challenge.OtpToken);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var userId = database.Users.Single(candidate => candidate.Email == email).Id;
        Assert.Empty(database.Notifications.Where(notification =>
            notification.UserId == userId
            && notification.Kind == NotificationKind.CredentialSignInOtpSent));
    }

    [Fact]
    public async Task Verify_otp_with_a_wrong_code_returns_400()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);
        var realCode = GetActiveCode(email, AccountCodePurpose.SignInOtp);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-otp",
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
            await _client.PostAsJsonAsync("/api/v1/app/auth/verify-otp", wrong);
        }

        var capped = await _client.PostAsJsonAsync("/api/v1/app/auth/verify-otp", wrong);
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
            "/api/v1/app/auth/verify-totp",
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
            "/api/v1/app/auth/verify-totp",
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
            "/api/v1/app/auth/verify-totp",
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
            "/api/v1/app/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = first.MfaToken!, Code = totp });
        Assert.Equal(HttpStatusCode.OK, firstVerify.StatusCode);

        var second = await ExpectChallengeAsync(adminEmail, Password, SignInAudience.Cp);
        var replay = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-totp",
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
            "/api/v1/app/auth/verify-totp",
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
            "/api/v1/app/auth/verify-otp",
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
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        // D-373 — registration enables 2FA; disable it to model the
        // admin-disabled account, where the API short-circuits to tokens.
        DisableTwoFactor(email);

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
    public async Task SignIn_for_a_verified_visitor_without_a_profile_surfaces_EmailVerified()
    {
        // D-198 — a verified user who hasn't completed their profile gets
        // AccountStateInfo.State = EmailVerified so the client routes them to
        // the profile form. The direct-issue path (where the info surfaces)
        // needs 2FA off — D-373 enables it at registration, so disable it to
        // model the admin-disabled account; the account_state JWT claim
        // carries the same signal on the email-OTP path.
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        DisableTwoFactor(email);

        var response = await SignInAsync(email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.False(body.Data!.MfaRequired);
        Assert.NotNull(body.Data.AccountState);
        Assert.Equal(nameof(AccountState.EmailVerified), body.Data.AccountState!.State);
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
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        // D-373 — registration enables 2FA; this test models the
        // admin-disabled account.
        DisableTwoFactor(email);

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
    public async Task SignIn_for_a_pending_admin_succeeds_with_AccountStateInfo_PendingApproval()
    {
        // P10 — D-051: the old 403 AUTH_ACCOUNT_NOT_APPROVED is gone. A
        // pending admin can sign in to the CP and gets tokens + an
        // AccountStateInfo on the response. The JWT carries
        // account_state=PendingApproval so the P11 authorization
        // handler will gate every endpoint except the pending page.
        // 2FA is off so the password step completes immediately and the
        // AccountStateInfo lands on the SignInResponse (D-033 / P10 plan).
        var (adminEmail, _) = await CreateAdminAsync();
        DisableTwoFactor(adminEmail);
        SetAccountState(adminEmail, AccountState.PendingApproval);

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        Assert.True(body!.Success);
        Assert.NotNull(body.Data!.AccountState);
        Assert.Equal("PendingApproval", body.Data.AccountState!.State);
        Assert.True(AuditEntryExists(adminEmail, AuditEvents.SignInAsGuest));
    }

    [Fact]
    public async Task SignIn_for_a_rejected_user_succeeds_with_AccountStateInfo_carrying_the_reason()
    {
        // P10 — D-051: rejected users also get tokens + AccountStateInfo
        // including the bilingual rejection reason persisted on the
        // user row (by RejectAsync on the admin endpoint).
        var (adminEmail, _) = await CreateAdminAsync();
        DisableTwoFactor(adminEmail);
        SetAccountState(adminEmail, AccountState.Rejected);
        SetRejectionReason(adminEmail,
            "Identity could not be verified after two follow-up calls.",
            "تعذّر التحقق من الهوية بعد محاولتي اتصال متابعة.");

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        Assert.True(body!.Success);
        Assert.NotNull(body.Data!.AccountState);
        Assert.Equal("Rejected", body.Data.AccountState!.State);
        Assert.Contains("Identity could not be verified",
            body.Data.AccountState.RejectionReason ?? string.Empty);
        Assert.Contains("تعذّر التحقق",
            body.Data.AccountState.RejectionReasonArabic ?? string.Empty);
        Assert.True(AuditEntryExists(adminEmail, AuditEvents.SignInAsGuest));
    }

    [Fact]
    public async Task SignIn_for_an_approved_user_returns_AccountStateInfo_null()
    {
        // Sanity — approved sign-ins keep the clean shape (no state info
        // on the response; the account_state JWT claim says "Approved").
        var (adminEmail, _) = await CreateAdminAsync();
        DisableTwoFactor(adminEmail);

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        Assert.True(body!.Success);
        Assert.Null(body.Data!.AccountState);
    }

    [Fact]
    public async Task JWT_for_a_pending_admin_carries_account_state_and_user_type_claims()
    {
        // P11 — D-052: the cookie-side routing relies on the JWT claims
        // surfaced through /auth/complete. Pin the wire shape here.
        var (adminEmail, _) = await CreateAdminAsync();
        DisableTwoFactor(adminEmail);
        SetAccountState(adminEmail, AccountState.PendingApproval);

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Cp);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        var accessToken = body!.Data!.Tokens!.AccessToken;

        var claims = DecodeJwtClaims(accessToken);
        Assert.Equal("PendingApproval", claims["account_state"]);
        Assert.Equal("Admin", claims["user_type"]);
    }

    [Fact]
    public async Task JWT_for_a_rejected_user_carries_account_state_Rejected()
    {
        var (adminEmail, _) = await CreateAdminAsync();
        DisableTwoFactor(adminEmail);
        SetAccountState(adminEmail, AccountState.Rejected);
        SetRejectionReason(adminEmail, "Reason text.", "نص السبب.");

        var response = await SignInAsync(adminEmail, Password, SignInAudience.Cp);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        var accessToken = body!.Data!.Tokens!.AccessToken;

        var claims = DecodeJwtClaims(accessToken);
        Assert.Equal("Rejected", claims["account_state"]);
        Assert.Contains("user_type", claims.Keys);
    }

    private static Dictionary<string, string> DecodeJwtClaims(string accessToken)
    {
        // No signature validation — this is for asserting the shape only.
        var middle = accessToken.Split('.')[1];
        var padded = middle.PadRight(
            middle.Length + (4 - middle.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                pairs[prop.Name] = prop.Value.GetString()!;
            }
        }
        return pairs;
    }

    [Fact]
    public async Task SignIn_for_a_pending_visitor_on_Web_is_allowed()
    {
        // Per D-010 / P4 — a pending visitor can sign in to Web and sees a
        // "pending" UI on their profile; only the CP surface blocks pending.
        var email = await RegisterVerifiedVisitorAsync();
        SetAccountState(email, AccountState.PendingApproval);

        var response = await SignInAsync(email, Password, SignInAudience.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    // ----------------------------------------------------------------------
    // H4 — D-059 + D-206: PasswordChangeRequired is enforced at sign-in. The
    // Control Panel now hands the operator a single-use password-change ticket
    // (in place of the old 403) so they can set a new password in-flow; every
    // other audience keeps the 403. The flag is still enforced at every later
    // token-mint path (see the refresh test below), and clearing it lets the
    // normal challenge run.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Sign_in_with_forced_change_returns_a_password_change_ticket_for_the_Control_Panel()
    {
        var (email, _) = await CreateAdminAsync();
        SetPasswordChangeRequired(email, value: true);

        var response = await SignInAsync(email, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success);
        Assert.NotNull(body.Data!.PasswordChangeToken);
        Assert.NotEmpty(body.Data.PasswordChangeToken!);
        // No session is minted yet — the ticket is the only thing returned.
        Assert.False(body.Data.MfaRequired);
        Assert.Null(body.Data.Tokens);
        Assert.True(AuditEntryExists(email, AuditEvents.SignInPasswordChangeTicketIssued));
    }

    [Fact]
    public async Task Sign_in_with_forced_change_is_still_blocked_for_non_Control_Panel_audiences()
    {
        // D-206 leaves mobile/web unchanged: a flagged visitor still gets the 403.
        var email = await RegisterVerifiedVisitorAsync();
        SetPasswordChangeRequired(email, value: true);

        var response = await SignInAsync(email, Password, SignInAudience.Web);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.AuthPasswordChangeRequired, body.Error!.Code);
    }

    [Fact]
    public async Task Complete_password_change_with_the_ticket_clears_the_flag_and_lets_sign_in_proceed()
    {
        var (email, _) = await CreateAdminAsync();
        SetPasswordChangeRequired(email, value: true);

        var signIn = await SignInAsync(email, Password, SignInAudience.Cp);
        var ticket = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!
            .Data!.PasswordChangeToken;
        Assert.NotNull(ticket);

        const string newPassword = "N3wPassw0rd!";
        var change = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/complete-password-change",
            new CompletePasswordChangeRequest
            {
                PasswordChangeToken = ticket!,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var changeBody =
            (await change.Content.ReadFromJsonAsync<ApiResult<CompletePasswordChangeResponse>>())!;
        Assert.True(changeBody.Success);
        Assert.True(changeBody.Data!.PasswordChanged);

        // The flag is cleared and the new password now reaches the second factor.
        var after = await SignInAsync(email, newPassword, SignInAudience.Cp);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterBody = (await after.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(afterBody.Data!.MfaRequired);
        Assert.Null(afterBody.Data.PasswordChangeToken);
    }

    [Fact]
    public async Task Complete_password_change_rejects_an_unknown_ticket()
    {
        var change = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/complete-password-change",
            new CompletePasswordChangeRequest
            {
                PasswordChangeToken = "not-a-real-ticket",
                NewPassword = "N3wPassw0rd!",
                ConfirmPassword = "N3wPassw0rd!",
            });

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        var body = await change.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.AuthMfaTokenInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Sign_in_succeeds_once_PasswordChangeRequired_is_cleared()
    {
        // A user the seeder marked, but operations subsequently cleared
        // (e.g. they completed the reset flow). The gate must let them
        // through — the flag is the only difference from the H4-blocked
        // case above.
        var (email, _) = await CreateAdminAsync();
        // CreateAdminAsync defaults the flag to false; assert that path
        // still goes through the existing 2FA challenge (sanity for the
        // gate not regressing the happy path).
        SetPasswordChangeRequired(email, value: false);

        var response = await SignInAsync(email, Password, SignInAudience.Cp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success);
        Assert.True(body.Data!.MfaRequired);
    }

    private void SetPasswordChangeRequired(string email, bool value)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.PasswordChangeRequired = value;
        database.SaveChanges();
    }

    // ----------------------------------------------------------------------
    // H19 — D-080: PasswordChangeRequired enforcement extended to every
    // token-mint path. The pre-H19 H4 gate only checked at the password
    // step, so a user holding a refresh token could rotate forever after
    // an admin set the flag.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Refresh_is_blocked_when_PasswordChangeRequired_is_set_after_sign_in()
    {
        // Sign in as a 2FA-disabled visitor to mint a refresh token quickly.
        var email = $"flag-refresh-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            await users.CreateAsync(
                new SimfUser
                {
                    UserName = email, Email = email, EmailConfirmed = true,
                    DisplayName = "Refresh Block Test",
                    AccountState = AccountState.Approved,
                    UserType = UserType.Visitor,
                },
                Password);
        }
        var signInResponse = await SignInAsync(email, Password, SignInAudience.Web);
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        var signInBody = (await signInResponse.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.NotNull(signInBody.Data!.Tokens);
        var refreshToken = signInBody.Data.Tokens!.RefreshToken;

        // Now the admin (or seeder, or scheduled operation) flips
        // PasswordChangeRequired on the user — the H19 contract says the
        // existing refresh token must STOP working.
        SetPasswordChangeRequired(email, value: true);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh",
            new RefreshRequest { RefreshToken = refreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, refreshResponse.StatusCode);
        var body = await refreshResponse.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthPasswordChangeRequired, body!.Error!.Code);
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
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = password, Audience = audience });

    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
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
            "/api/v1/app/auth/verify-email",
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
            UserType = UserType.Admin,   // P7b — audience gate keys off UserType
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
        return AuthFlow.RecoverPlaintextCode(database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code);
    }

    private void SetAccountState(string email, AccountState state)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.AccountState = state;
        database.SaveChanges();
    }

    private void SetRejectionReason(string email, string reason, string reasonArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDatabase = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        // D-106 / D-167: rejection text lives on UserProfile (App DB).
        var profile = appDatabase.UserProfiles.SingleOrDefault(p => p.UserId == user.Id);
        if (profile is null)
        {
            profile = new SIMF.Domain.Profiles.UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = SimfClock.Now,
            };
            appDatabase.UserProfiles.Add(profile);
        }
        profile.RejectionReason = reason;
        profile.RejectionReasonArabic = reasonArabic;
        user.StateChangedAt = SimfClock.Now;
        database.SaveChanges();
        appDatabase.SaveChanges();
    }

    private bool AuditEntryExists(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog.Any(
            entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }
}
