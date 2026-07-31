// #2 (Q1, 2026-07-30) — ENROLMENT-FIRST for the Control Panel, plus #2d
// (force TwoFactorEnabled on admin creation), which cannot ship without it.
//
// The defect: SignInService's `if (!user.TwoFactorEnabled)` fast path issued a
// full access token on the password alone with no branch on the audience, so a
// Control Panel admin with 2FA off — which is what the production super-admin
// is recorded as — held an admin session after one factor.
//
// The trap: simply forcing the flag (#2d) makes it WORSE. The factor selector
// picks `authenticatorKey != "" || roles.Count > 0 ? Totp : EmailOtp`, so a new
// admin (role, no key) is challenged for a TOTP code against a secret that does
// not exist and can never sign in. Enrolment-first is what makes #2d safe, and
// that is why the last test here asserts the newly created admin actually gets
// IN — a test that only checked the flag would pass while every new admin was
// bricked.
//
// The gate is ON in this class (ControlPanelTwoFactorApiFactory), which is the
// production default — and, since 2026-07-31, in the general suite too: the
// ~150 admin fixtures there enrol through AuthFlow.SignInControlPanelAsync and
// complete a real TOTP step instead of relying on a pinned-off gate. The
// enrolment CONTRACT (challenge shape, ticket single-use, wrong-code refusal)
// is still proved only here.
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ControlPanelTwoFactorEnrolmentTests
    : IClassFixture<ControlPanelTwoFactorApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly ControlPanelTwoFactorApiFactory _factory;
    private readonly HttpClient _client;

    public ControlPanelTwoFactorEnrolmentTests(ControlPanelTwoFactorApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>#2, the defect itself. A Cp-audience password step for an
    /// account with no authenticator secret must hand back an ENROLMENT ticket
    /// and NOTHING else — no access token, no refresh token, no MFA ticket that
    /// could be verified against a secret that does not exist.</summary>
    [Fact]
    public async Task A_Control_Panel_password_step_with_no_authenticator_returns_an_enrolment_challenge_not_tokens()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: false);

        var body = await SignInAsync(email, SignInAudience.Cp);

        Assert.True(body.Success);
        Assert.NotNull(body.Data!.TwoFactorEnrolmentToken);
        Assert.NotEmpty(body.Data.TwoFactorEnrolmentToken!);
        Assert.Null(body.Data.Tokens);
        Assert.Null(body.Data.MfaToken);
        Assert.Null(body.Data.OtpToken);
        Assert.False(body.Data.MfaRequired);
    }

    /// <summary>The gate is scoped to the Control Panel. A visitor on the App
    /// audience with 2FA off still completes on the password alone (D-033), so
    /// the fix cannot have silently changed the mobile contract.</summary>
    [Fact]
    public async Task The_App_audience_is_unaffected_by_the_Control_Panel_enrolment_gate()
    {
        var email = await CreateApprovedVisitorAsync();

        var body = await SignInAsync(email, SignInAudience.App);

        Assert.True(body.Success);
        Assert.Null(body.Data!.TwoFactorEnrolmentToken);
        Assert.NotNull(body.Data.Tokens);
        Assert.NotEmpty(body.Data.Tokens!.AccessToken);
    }

    /// <summary>The whole point of enrolment-first: the challenge is a way IN,
    /// not a wall. Enrol against the ticket, confirm the first code, and the
    /// session the password step withheld is issued — stamped <c>amr=mfa</c>,
    /// because the code just verified IS the second factor.</summary>
    [Fact]
    public async Task Enrolling_from_the_challenge_completes_the_sign_in_with_an_mfa_stamped_token()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: false);
        var challenge = (await SignInAsync(email, SignInAudience.Cp)).Data!;

        var setup = await StartEnrolmentAsync(challenge.TwoFactorEnrolmentToken!);
        Assert.StartsWith("otpauth://totp/", setup.OtpAuthUri);
        Assert.Contains("<svg", setup.QrCodeSvg);

        var completed = await CompleteEnrolmentAsync(
            challenge.TwoFactorEnrolmentToken!, ComputeCode(setup.Secret));

        Assert.NotEmpty(completed.Tokens.AccessToken);
        Assert.Equal("mfa", ClaimValue(completed.Tokens.AccessToken, "amr"));
        // D-040 — the one-time recovery codes travel with the completed
        // enrolment, so the admin is never left with a single device.
        Assert.Equal(10, completed.RecoveryCodes.Count);

        // The account is genuinely enrolled afterwards, not just let through.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
        Assert.False(string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)));
    }

    /// <summary>The token minted through enrolment is a real session, not a
    /// stub: it opens the admin surface the way any other admin token does.</summary>
    [Fact]
    public async Task The_session_issued_by_enrolment_reaches_the_admin_surface()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: false);
        var challenge = (await SignInAsync(email, SignInAudience.Cp)).Data!;
        var setup = await StartEnrolmentAsync(challenge.TwoFactorEnrolmentToken!);
        var completed = await CompleteEnrolmentAsync(
            challenge.TwoFactorEnrolmentToken!, ComputeCode(setup.Secret));

        var allowed = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), completed.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    /// <summary>No regression for the admins who already have an authenticator:
    /// they keep the ordinary TOTP challenge and never see enrolment.</summary>
    [Fact]
    public async Task An_already_enrolled_Control_Panel_admin_still_gets_the_normal_TOTP_challenge()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: true);

        var body = await SignInAsync(email, SignInAudience.Cp);

        Assert.True(body.Data!.MfaRequired);
        Assert.NotNull(body.Data.MfaToken);
        Assert.Null(body.Data.TwoFactorEnrolmentToken);
        Assert.Null(body.Data.Tokens);
    }

    /// <summary>A wrong first code must not let anyone through — the enrolment
    /// step is a second factor, so failing it fails the sign-in.</summary>
    [Fact]
    public async Task A_wrong_confirmation_code_issues_no_session()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: false);
        var challenge = (await SignInAsync(email, SignInAudience.Cp)).Data!;
        await StartEnrolmentAsync(challenge.TwoFactorEnrolmentToken!);

        var response = await PostAsync(
            "/api/v1/app/auth/totp/enrolment/complete",
            new CompleteTwoFactorEnrolmentRequest
            {
                EnrolmentToken = challenge.TwoFactorEnrolmentToken!,
                Code = "000000",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.TotpEnrolmentCodeInvalid, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.False(await users.GetTwoFactorEnabledAsync(user));
    }

    /// <summary>One ticket, one session. A replayed enrolment ticket must not
    /// mint a second token — the same single-use rule the TOTP and OTP tickets
    /// carry.</summary>
    [Fact]
    public async Task An_enrolment_ticket_cannot_be_spent_twice()
    {
        var email = await CreateAdminAsync(enrolAuthenticator: false);
        var challenge = (await SignInAsync(email, SignInAudience.Cp)).Data!;
        var setup = await StartEnrolmentAsync(challenge.TwoFactorEnrolmentToken!);
        await CompleteEnrolmentAsync(
            challenge.TwoFactorEnrolmentToken!, ComputeCode(setup.Secret));

        var replay = await PostAsync(
            "/api/v1/app/auth/totp/enrolment/start",
            new StartTwoFactorEnrolmentRequest
            {
                EnrolmentToken = challenge.TwoFactorEnrolmentToken!,
            });

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        var body = (await replay.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthTwoFactorEnrolmentRequired, body.Error!.Code);
    }

    /// <summary>An unknown ticket is refused. Without this the enrolment
    /// endpoints would be an unauthenticated "stage me a secret on any
    /// account" surface.</summary>
    [Fact]
    public async Task An_unknown_enrolment_ticket_is_refused()
    {
        var response = await PostAsync(
            "/api/v1/app/auth/totp/enrolment/start",
            new StartTwoFactorEnrolmentRequest { EnrolmentToken = "not-a-ticket" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthTwoFactorEnrolmentRequired, body.Error!.Code);
    }

    // ---------------------------------------------------------------------
    // #2d — admin creation forces two-factor
    // ---------------------------------------------------------------------

    /// <summary>#2d, first half — a CP-provisioned admin is created with
    /// <c>TwoFactorEnabled</c> set, so it can never end up permanently
    /// single-factor.</summary>
    [Fact]
    public async Task A_newly_created_admin_is_marked_two_factor_enabled()
    {
        var (_, newEmail) = await CreateAdminThroughTheAdminApiAsync();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var created = (await users.FindByEmailAsync(newEmail))!;

        Assert.True(await users.GetTwoFactorEnabledAsync(created));
    }

    /// <summary>#2d, second half — and THE point. The flag alone would brick
    /// every new admin (role + no key selects a TOTP challenge against a secret
    /// that does not exist). Assert the admin actually gets in, through the
    /// enrolment path #2 added.</summary>
    [Fact]
    public async Task A_newly_created_admin_can_complete_a_first_sign_in_through_enrolment()
    {
        var (adminToken, newEmail) = await CreateAdminThroughTheAdminApiAsync();
        const string chosenPassword = "NewPassw0rd!";

        // The invite email carries a reset code; the new admin sets their password.
        await ResetPasswordFromInviteAsync(newEmail, chosenPassword);

        // P4 — invited staff land in PendingApproval; approve before signing in.
        var userId = await UserIdAsync(newEmail);
        var approve = await PostAuthAsync(
            $"/api/v1/admin/admins/{userId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var challenge = (await SignInAsync(
            newEmail, SignInAudience.Cp, chosenPassword)).Data!;
        Assert.NotNull(challenge.TwoFactorEnrolmentToken);
        Assert.Null(challenge.Tokens);

        var setup = await StartEnrolmentAsync(challenge.TwoFactorEnrolmentToken!);
        var completed = await CompleteEnrolmentAsync(
            challenge.TwoFactorEnrolmentToken!, ComputeCode(setup.Secret));

        Assert.NotEmpty(completed.Tokens.AccessToken);
        Assert.Equal("mfa", ClaimValue(completed.Tokens.AccessToken, "amr"));
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    /// <summary>Creates an Administrator directly in the store. When
    /// <paramref name="enrolAuthenticator"/> is true it also pairs an
    /// authenticator secret and turns 2FA on, modelling an admin who enrolled
    /// before this change.</summary>
    private async Task<string> CreateAdminAsync(bool enrolAuthenticator)
    {
        var email = $"cp2fa-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            await roleManager.CreateAsync(
                new SimfRole { Name = AdministratorRole, IsBaseline = true });
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "CP 2FA Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        await users.AddToRoleAsync(user, AdministratorRole);

        if (enrolAuthenticator)
        {
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
        }
        return email;
    }

    private async Task<string> CreateApprovedVisitorAsync()
    {
        var email = $"cp2fa-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        await users.CreateAsync(
            new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "CP 2FA Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            },
            AuthFlow.Password);
        return email;
    }

    /// <summary>An enrolled Administrator token, obtained the way production
    /// does it: password step -> TOTP challenge -> verify. With the gate on
    /// there is no password-only shortcut, which is the point.</summary>
    private async Task<string> SignedInAdministratorTokenAsync()
    {
        var email = $"cp2fa-actor-{Guid.NewGuid():N}@simf.test";
        string secret;
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(
                    new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "CP 2FA Actor",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            secret = (await users.GetAuthenticatorKeyAsync(user))!;
        }

        var challenge = (await SignInAsync(email, SignInAudience.Cp)).Data!;
        var verify = await PostAsync(
            "/api/v1/app/auth/verify-totp",
            new VerifyTotpRequest
            {
                MfaToken = challenge.MfaToken!,
                Code = ComputeCode(secret),
            });
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!;
        Assert.True(
            tokens.Data?.AccessToken is not null,
            "The enrolled-admin fixture did not complete its TOTP challenge.");
        return tokens.Data!.AccessToken;
    }

    private async Task<(string ActorToken, string NewAdminEmail)> CreateAdminThroughTheAdminApiAsync()
    {
        var actorToken = await SignedInAdministratorTokenAsync();
        var newEmail = $"cp2fa-created-{Guid.NewGuid():N}@simf.test";

        var created = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = newEmail,
                DisplayName = "Created Admin",
                Roles = [AdministratorRole],
            },
            actorToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return (actorToken, newEmail);
    }

    private async Task ResetPasswordFromInviteAsync(string email, string newPassword)
    {
        string inviteCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
            var hash = await db.AccountCodes
                .Where(code => code.UserId == user.Id
                    && code.Purpose == AccountCodePurpose.PasswordReset
                    && code.ConsumedAt == null)
                .OrderByDescending(code => code.CreatedAt)
                .Select(code => code.Code)
                .FirstAsync();
            inviteCode = AuthFlow.RecoverPlaintextCode(hash);
        }

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = inviteCode,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        return user.Id;
    }

    private async Task<ApiResult<SignInResponse>> SignInAsync(
        string email, SignInAudience audience, string password = AuthFlow.Password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = password, Audience = audience });
        return (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
    }

    private async Task<TotpSetupResponse> StartEnrolmentAsync(string enrolmentToken)
    {
        var response = await PostAsync(
            "/api/v1/app/auth/totp/enrolment/start",
            new StartTwoFactorEnrolmentRequest { EnrolmentToken = enrolmentToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!;
        return body.Data!;
    }

    private async Task<CompleteTwoFactorEnrolmentResponse> CompleteEnrolmentAsync(
        string enrolmentToken, string code)
    {
        var response = await PostAsync(
            "/api/v1/app/auth/totp/enrolment/complete",
            new CompleteTwoFactorEnrolmentRequest
            {
                EnrolmentToken = enrolmentToken,
                Code = code,
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<CompleteTwoFactorEnrolmentResponse>>())!;
        return body.Data!;
    }

    private static string ComputeCode(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp(DateTime.UtcNow);

    private static string? ClaimValue(string accessToken, string claimType) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken)
            .Claims
            .FirstOrDefault(claim => claim.Type == claimType)?.Value;

    private Task<HttpResponseMessage> PostAsync<TBody>(string url, TBody body) =>
        _client.PostAsJsonAsync(url, body);

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
