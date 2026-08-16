using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Part B integration tests for <c>POST /api/v1/app/auth/badge-sign-in</c> — a
/// returning holder scans their badge and completes sign-in with only their
/// password. The QR selects the account; the password (plus any 2FA / lockout)
/// runs through the real sign-in pipeline, so the response is identical to email
/// sign-in. The core security property is that an unknown QR, a passwordless
/// account, and a not-yet-approved account all return the SAME generic 401 as a
/// wrong password — the public badge is never a valid-QR oracle and never
/// substitutes for the password.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Badges)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class BadgeSignInTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    // The single generic invalid-credentials response the service returns for a
    // wrong password, an unknown QR, a passwordless account and a non-approved
    // account alike — asserted identical across every branch so none of them is
    // distinguishable from a wrong password.
    private const string GenericInvalidEnglish = "The email address or password is not correct.";
    private const string GenericInvalidArabic = "البريد الإلكتروني أو كلمة المرور غير صحيحة.";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeSignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- (a) happy path, 2FA off ----------------------------------------------

    [Fact]
    public async Task Badge_sign_in_with_the_correct_password_and_2fa_off_issues_tokens()
    {
        // A returning holder with a password on file and 2FA off (the default for
        // a UserManager-created account) — the password step IS the sign-in, so
        // the badge scan + password issue tokens directly.
        var (_, qrId) = await CreateApprovedVisitorAsync(
            withPassword: true, email: $"pw-{Guid.NewGuid():N}@example.com");

        var response = await BadgeSignInAsync(qrId, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success);
        Assert.False(body.Data!.MfaRequired);
        Assert.Null(body.Data.OtpToken);
        Assert.Null(body.Data.MfaToken);
        Assert.NotNull(body.Data.Tokens);
        Assert.NotEmpty(body.Data.Tokens!.AccessToken);
        Assert.NotEmpty(body.Data.Tokens.RefreshToken);
        Assert.Equal("Bearer", body.Data.Tokens.TokenType);
    }

    // -- (b) 2FA on: challenge then verify-otp completion ----------------------

    [Fact]
    public async Task Badge_sign_in_for_a_2fa_visitor_returns_the_email_otp_challenge()
    {
        var email = $"pw-{Guid.NewGuid():N}@example.com";
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: true, email: email);
        // The badge delegates to the real sign-in pipeline with the App audience;
        // a visitor with no authenticator key completes 2FA with an emailed code.
        AuthFlow.EnableTwoFactor(_factory, email);

        var response = await BadgeSignInAsync(qrId, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success);
        Assert.True(body.Data!.MfaRequired);
        Assert.NotNull(body.Data.OtpToken);
        Assert.Null(body.Data.Tokens);
    }

    [Fact]
    public async Task Badge_sign_in_for_a_2fa_visitor_completes_with_the_emailed_code()
    {
        var email = $"pw-{Guid.NewGuid():N}@example.com";
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: true, email: email);
        AuthFlow.EnableTwoFactor(_factory, email);

        var challenge = (await (await BadgeSignInAsync(qrId, Password)).Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        Assert.NotNull(challenge.OtpToken);

        // Recover the emailed sign-in code the same way SignInTests does, then
        // finish the challenge on the shared verify-otp endpoint.
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.SignInOtp),
            });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(email, tokens.User.Email);
    }

    // -- (c) wrong password ---------------------------------------------------

    [Fact]
    public async Task Badge_sign_in_with_a_wrong_password_returns_the_generic_401_and_counts_the_failure()
    {
        var email = $"pw-{Guid.NewGuid():N}@example.com";
        var (userId, qrId) = await CreateApprovedVisitorAsync(withPassword: true, email: email);

        var response = await BadgeSignInAsync(qrId, "Wrong1!Password");

        await AssertGenericInvalidCredentialsAsync(response);
        // The real sign-in pipeline recorded the failed attempt against the
        // account (the same increment that drives Identity lockout).
        Assert.Equal(1, GetAccessFailedCount(userId));
    }

    // -- (d) unknown QR — indistinguishable from a wrong password -------------

    [Fact]
    public async Task Badge_sign_in_with_an_unknown_qr_returns_the_same_generic_401()
    {
        var response = await BadgeSignInAsync("ZZZZZZZZZZZZ", Password);

        await AssertGenericInvalidCredentialsAsync(response);
    }

    // -- (e) passwordless (activation-pending) account ------------------------

    [Fact]
    public async Task Badge_sign_in_for_a_passwordless_account_returns_the_same_generic_401()
    {
        // A walk-in registered without a password (activation still pending). The
        // QR resolves to an approved account, but it has no password hash, so the
        // real pipeline fails at the password check with the generic error — the
        // badge can never substitute for the password.
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: null);

        var response = await BadgeSignInAsync(qrId, Password);

        await AssertGenericInvalidCredentialsAsync(response);
    }

    // -- (f) non-approved account ---------------------------------------------

    [Fact]
    public async Task Badge_sign_in_for_a_non_approved_account_returns_the_same_generic_401()
    {
        // Even with the CORRECT password, a not-yet-approved account never
        // resolves (the QR resolver requires Approved), so it returns the same
        // generic 401 — a non-approved badge is indistinguishable from a bad one.
        var email = $"pw-{Guid.NewGuid():N}@example.com";
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: true, email: email);
        AuthFlow.SetAccountState(_factory, email, AccountState.PendingApproval);

        var response = await BadgeSignInAsync(qrId, Password);

        await AssertGenericInvalidCredentialsAsync(response);
    }

    // -- (g) lockout ----------------------------------------------------------

    [Fact]
    public async Task Badge_sign_in_locks_the_account_after_five_wrong_passwords()
    {
        var email = $"pw-{Guid.NewGuid():N}@example.com";
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: true, email: email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bad = await BadgeSignInAsync(qrId, "Wrong1!Password");
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var locked = await BadgeSignInAsync(qrId, "Wrong1!Password");
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        var lockedBody = (await locked.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthAccountLocked, lockedBody.Error!.Code);

        // The correct password is refused too while the account is locked.
        var correctButLocked = await BadgeSignInAsync(qrId, Password);
        Assert.Equal(HttpStatusCode.Locked, correctButLocked.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> BadgeSignInAsync(string qrId, string password) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-sign-in",
            new BadgeSignInRequest { QrId = qrId, Password = password });

    private async Task AssertGenericInvalidCredentialsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, body.Error!.Code);
        // The message is identical to the wrong-password message, so a caller
        // cannot tell an unknown / passwordless / non-approved QR apart from a
        // simple wrong password.
        Assert.Equal(GenericInvalidEnglish, body.Error.Message);
        Assert.Equal(GenericInvalidArabic, body.Error.MessageArabic);
    }

    private int GetAccessFailedCount(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return idDb.Users.Single(u => u.Id == userId).AccessFailedCount;
    }

    /// <summary>Creates an Approved visitor with a minted QR id, optionally with
    /// a password, optionally with a real email (null = synthesized placeholder,
    /// mirroring a walk-in registered without one). Returns (userId, qrId). Kept
    /// local to this class (the BadgeAuthTests copy is private); reuses the
    /// shared <see cref="SimfApiFactory"/> + <see cref="AuthFlow"/> harness.</summary>
    private async Task<(Guid UserId, string QrId)> CreateApprovedVisitorAsync(
        bool withPassword, string? email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var loginEmail = email ?? $"walkin-{Guid.NewGuid():N}@simf.local";
        var user = new SimfUser
        {
            UserName = loginEmail,
            Email = loginEmail,
            EmailConfirmed = email is not null,
            DisplayName = "Badge Holder",
            UserType = UserType.Visitor,
            AccountState = AccountState.Approved,
        };
        var create = withPassword
            ? await users.CreateAsync(user, Password)
            : await users.CreateAsync(user);
        Assert.True(create.Succeeded, string.Join(";", create.Errors.Select(e => e.Description)));

        var qrId = NewQrId();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Badge Holder",
            NameArabic = "حامل الشارة",
            QrId = qrId,
            // Badge sign-in reads admission on the PROFILE, so approving the
            // account alone leaves the badge refused.
            AdmissionState = AccountState.Approved,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return (user.Id, qrId);
    }

    private static string NewQrId()
    {
        // 12-char Crockford-base32 id, derived deterministically from a fresh
        // Guid so each test row is unique (matches the minter's alphabet/length).
        var guid = Guid.NewGuid().ToByteArray();
        var chars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            chars[i] = Alphabet[guid[i] % Alphabet.Length];
        }
        return new string(chars);
    }
}
