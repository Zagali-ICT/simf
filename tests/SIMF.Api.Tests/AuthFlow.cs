using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Shared helpers that drive the full authentication flow in integration tests.</summary>
internal static class AuthFlow
{
    public const string Password = "Zx9#mKp2!";

    /// <summary>
    /// Signs a brand-new visitor up, verifies the email, and ensures two-factor
    /// authentication is on so the sign-in OTP path runs. Returns the address.
    /// D-373 made 2FA the registration DEFAULT, so the explicit enable below is
    /// now a belt-and-braces no-op kept for clarity (it also documents the
    /// pre-D-373 reason this helper exists — D-033 had it off by default).
    /// </summary>
    public static async Task<string> RegisterVerifiedVisitorAsync(
        HttpClient client,
        SimfApiFactory factory)
    {
        var email = $"flow-{Guid.NewGuid():N}@simf.test";

        await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        await client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(factory, email, AccountCodePurpose.EmailVerification),
            });
        EnableTwoFactor(factory, email);
        return email;
    }

    /// <summary>Turns two-factor authentication on for the account, directly in the database.</summary>
    public static void EnableTwoFactor(SimfApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.TwoFactorEnabled = true;
        database.SaveChanges();
    }

    /// <summary>Turns two-factor authentication OFF for the account, directly in
    /// the database — the admin-disabled scenario, the only 2FA-off path now
    /// that D-373 activates it for every new registration.</summary>
    public static void DisableTwoFactor(SimfApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.TwoFactorEnabled = false;
        database.SaveChanges();
    }

    /// <summary>
    /// Mints a Control Panel access token for an account the caller has already
    /// created, the way a real operator gets one.
    /// </summary>
    /// <remarks>
    /// #2 (Q1) — mandatory Control-Panel two-factor enrolment
    /// (<c>IdentityLifecycle:RequireControlPanelTwoFactorEnrolment</c>, ON by
    /// default and the production posture) means a Cp-audience password step
    /// NEVER hands back a session on its own: an account with no authenticator
    /// secret is answered with an enrolment ticket, an enrolled one with a TOTP
    /// challenge. So this helper enrols the account FIRST
    /// (<see cref="EnrolAuthenticatorAsync"/> — the same two UserManager calls the
    /// real enrolment path makes) and then answers the challenge with a genuine
    /// code computed from that secret. Every admin fixture in this assembly goes
    /// through here, so the ~150 admin tests exercise the shipping gate instead of
    /// a pinned-off one.
    /// </remarks>
    public static async Task<string> SignInControlPanelAsync(
        HttpClient client,
        SimfApiFactory factory,
        string email,
        string password = Password)
    {
        var secret = await EnrolAuthenticatorAsync(factory, email);

        var signIn = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = password,
                Audience = SignInAudience.Cp,
            });
        var challenge = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        if (challenge.Data is null)
        {
            throw new InvalidOperationException(
                $"The Control Panel password step failed for {email}: "
                + $"{(int)signIn.StatusCode} {challenge.Error?.Code}.");
        }

        // Tokens on the password step mean the enrolment gate is OFF. NO factory in
        // this assembly turns it off any more, so this is not a supported mode — it
        // is the regression this helper exists to prevent. Accepting the tokens here
        // (as an earlier draft did) would silently return every admin fixture to the
        // single-factor path and the whole suite would stay green while proving
        // nothing, so it fails loudly instead.
        if (challenge.Data.Tokens is not null)
        {
            throw new InvalidOperationException(
                $"The Control Panel password step for {email} returned tokens without a "
                + "second factor, so IdentityLifecycle:RequireControlPanelTwoFactorEnrolment "
                + "is OFF. That is the pre-fix path; see ControlPanelTwoFactorGatePinTests.");
        }

        var mfaToken = challenge.Data.MfaToken
            ?? throw new InvalidOperationException(
                $"The Control Panel password step for {email} returned neither tokens "
                + "nor a TOTP challenge, so the account was not enrolled as expected.");

        var verify = await client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = mfaToken, Code = ComputeTotpCode(secret) });
        var verified = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!;
        return verified.Data?.AccessToken
            ?? throw new InvalidOperationException(
                $"The TOTP step failed for {email}: "
                + $"{(int)verify.StatusCode} {verified.Error?.Code}.");
    }

    /// <summary>
    /// Pairs a fresh authenticator secret with the account and turns two-factor
    /// on — the same <c>UserManager</c> pair the real enrolment path and the
    /// super-admin seeder use — and returns the base32 secret so the caller can
    /// compute codes from it.
    /// </summary>
    /// <remarks>
    /// The TOTP replay guard is cleared alongside, exactly as the admin-driven
    /// 2FA reset does (<c>AdminAccountService</c>), because the secret handed back
    /// here is newly issued: without that, signing the SAME account in twice
    /// inside one 30-second time-step would read the second code as a replay of
    /// the first and fail. Replay itself is covered by
    /// <c>SignInTests.A_TOTP_code_cannot_be_used_twice</c>.
    /// </remarks>
    public static async Task<string> EnrolAuthenticatorAsync(
        SimfApiFactory factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException(
                $"No account exists for {email}, so it cannot be enrolled.");

        await users.ResetAuthenticatorKeyAsync(user);
        // Set before SetTwoFactorEnabledAsync — that call persists the tracked
        // entity, so the cleared guard is saved with the flag.
        user.LastUsedTotpTimestep = null;
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GetAuthenticatorKeyAsync(user))!;
    }

    /// <summary>
    /// Completes a mandatory Control-Panel two-factor enrolment from the ticket
    /// the password step handed back — start, compute the first code, confirm —
    /// and returns the session the password step withheld. This is the ceremony a
    /// first-time Control Panel operator actually performs.
    /// </summary>
    public static async Task<AuthTokens> CompleteTwoFactorEnrolmentAsync(
        HttpClient client,
        string enrolmentToken)
    {
        var start = await client.PostAsJsonAsync(
            "/api/v1/app/auth/totp/enrolment/start",
            new StartTwoFactorEnrolmentRequest { EnrolmentToken = enrolmentToken });
        var setup = (await start.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!;
        if (setup.Data is null)
        {
            throw new InvalidOperationException(
                "Two-factor enrolment could not be started: "
                + $"{(int)start.StatusCode} {setup.Error?.Code}.");
        }

        var complete = await client.PostAsJsonAsync(
            "/api/v1/app/auth/totp/enrolment/complete",
            new CompleteTwoFactorEnrolmentRequest
            {
                EnrolmentToken = enrolmentToken,
                Code = ComputeTotpCode(setup.Data.Secret),
            });
        var completed = (await complete.Content
            .ReadFromJsonAsync<ApiResult<CompleteTwoFactorEnrolmentResponse>>())!;
        return completed.Data?.Tokens
            ?? throw new InvalidOperationException(
                "Two-factor enrolment could not be completed: "
                + $"{(int)complete.StatusCode} {completed.Error?.Code}.");
    }

    /// <summary>The current authenticator code for a base32 secret.</summary>
    /// <remarks>
    /// TOTP counts 30-second steps from the UNIX epoch, so this is deliberately
    /// a zoned value. The rest of SIMF stores Saudi local wall-clock through
    /// <see cref="SimfClock"/>; using that here would put every code three hours
    /// out and no code would ever verify.
    /// </remarks>
    public static string ComputeTotpCode(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp(DateTime.UtcNow);

    /// <summary>
    /// Signs a brand-new visitor up, verifies the email and signs in with 2FA
    /// explicitly DISABLED (the admin-disabled scenario — D-373 turns 2FA on at
    /// every registration) — so the password step issues tokens directly.
    /// Returns the issued token pair, used by the TOTP-enrolment integration tests.
    /// </summary>
    public static async Task<AuthTokens> SignInVisitorWithoutTwoFactorAsync(
        HttpClient client,
        SimfApiFactory factory)
    {
        var email = $"flow-noTfa-{Guid.NewGuid():N}@simf.test";

        await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        await client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(factory, email, AccountCodePurpose.EmailVerification),
            });
        // D-373: registration now enables 2FA — switch it off to model the
        // admin-disabled account this helper exists for.
        DisableTwoFactor(factory, email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password });
        var envelope = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!;
    }

    /// <summary>
    /// Signs a brand-new visitor up, verifies the email, forces the account to
    /// <see cref="AccountState.Approved"/> and signs in with 2FA disabled — so the
    /// issued token carries the <c>account_state=Approved</c> claim that the
    /// <c>RequireApprovedAccount</c> policy checks (the claim is minted at sign-in,
    /// not read live, so the state must be set BEFORE sign-in). Returns the token pair.
    /// </summary>
    public static async Task<AuthTokens> SignInApprovedVisitorWithoutTwoFactorAsync(
        HttpClient client,
        SimfApiFactory factory)
    {
        var email = $"flow-approved-{Guid.NewGuid():N}@simf.test";

        await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        await client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(factory, email, AccountCodePurpose.EmailVerification),
            });
        // Approve BEFORE sign-in so the minted JWT carries account_state=Approved,
        // and disable 2FA so the password step issues tokens directly.
        SetAccountState(factory, email, AccountState.Approved);
        DisableTwoFactor(factory, email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password });
        var envelope = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!;
    }

    /// <summary>Signs a brand-new visitor in fully and returns the issued token pair.</summary>
    public static async Task<AuthTokens> SignInVisitorAsync(HttpClient client, SimfApiFactory factory)
    {
        var email = await RegisterVerifiedVisitorAsync(client, factory);

        var signIn = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password });
        var challenge = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;

        var verify = await client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = GetActiveCode(factory, email, AccountCodePurpose.SignInOtp),
            });
        return (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
    }

    /// <summary>The newest unconsumed code of the given purpose for the user.</summary>
    public static string GetActiveCode(
        SimfApiFactory factory,
        string email,
        AccountCodePurpose purpose)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        var storedHash = database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
        return RecoverPlaintextCode(storedHash);
    }

    /// <summary>M3 — recover the six-digit plaintext of a stored AccountCode
    /// hash against the configured HMAC key (the test process shares the same key
    /// as the host that stored it). Call this anywhere a test reads
    /// AccountCode.Code to obtain a usable OTP / verification code.</summary>
    /// <remarks>
    /// This used to scan the code space one candidate at a time — 10^6 keyed
    /// hashes worst case, 500,000 expected, about half a second per call by its
    /// own comment — and the suite calls it hundreds of times per run. The same
    /// scan now happens ONCE per process and is kept, so every later call is a
    /// dictionary lookup. The whole space fits: a stored hash is 16 hex chars,
    /// exactly 64 bits, so it parses losslessly into the ulong key and the table
    /// is exact rather than probabilistic.
    /// </remarks>
    public static string RecoverPlaintextCode(string storedHash)
    {
        if (!CodesByHash().TryGetValue(ParseHashKey(storedHash), out var code))
        {
            throw new InvalidOperationException(
                "Could not recover the plaintext code from its hash (test HMAC key mismatch).");
        }

        return code.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // The table is only valid for the key AccountCodeHasher currently holds, and
    // that key is installed from configuration at host start — so the hash of a
    // fixed probe code is recorded with the table and re-checked on every call.
    // A host that installed a different key changes the probe, and the table is
    // rebuilt rather than confidently returning the wrong code.
    private const string TableProbeCode = "000000";

    private static readonly object CodeTableGate = new();
    private static Dictionary<ulong, int>? _codesByHash;
    private static string? _codeTableProbe;

    private static Dictionary<ulong, int> CodesByHash()
    {
        var probe = SIMF.Application.IdentityAccess.AccountCodeHasher.Hash(TableProbeCode);

        lock (CodeTableGate)
        {
            if (_codesByHash is not null && _codeTableProbe == probe)
            {
                return _codesByHash;
            }

            var table = new Dictionary<ulong, int>(1_000_000);
            for (var candidate = 0; candidate < 1_000_000; candidate++)
            {
                var text = candidate.ToString(
                    "D6", System.Globalization.CultureInfo.InvariantCulture);
                table[ParseHashKey(
                    SIMF.Application.IdentityAccess.AccountCodeHasher.Hash(text))] = candidate;
            }

            _codesByHash = table;
            _codeTableProbe = probe;
            return table;
        }
    }

    private static ulong ParseHashKey(string storedHash) =>
        ulong.Parse(
            storedHash,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Forces an account into a given lifecycle state directly in the database,
    /// standing in for the admin approval queue.
    ///
    /// <para>It deliberately does NOT create the attendee profile that real
    /// approval stubs (<c>AdminAccountService.EnsureUserProfileAsync</c>): most
    /// callers here seed their own profile with the fields their test needs, and
    /// creating a second one collides with the filtered unique index over UserId.
    /// A test whose visitor must hold an attendee record — anything that books a
    /// seat, arrives at a hall or is scanned at a booth — calls
    /// <c>TestAttendeeProfiles.EnsureForAccountAsync</c> after this.</para>
    /// </summary>
    public static void SetAccountState(SimfApiFactory factory, string email, AccountState state)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.AccountState = state;
        database.SaveChanges();
    }

    /// <summary>Reads back the account's current state and email-confirmed flag.</summary>
    public static (AccountState State, bool EmailConfirmed) GetAccountState(
        SimfApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return (user.AccountState, user.EmailConfirmed);
    }

    /// <summary>True when an operation-log entry of the given type exists for the email.</summary>
    public static bool AuditEntryExists(SimfApiFactory factory, string email, string eventType)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog.Any(
            entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }

    /// <summary>Counts every code of the given purpose ever created for the user.</summary>
    public static int CodeCount(SimfApiFactory factory, string email, AccountCodePurpose purpose)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return database.AccountCodes.Count(
            code => code.UserId == user.Id && code.Purpose == purpose);
    }
}
