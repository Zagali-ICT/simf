using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Shared helpers that drive the full authentication flow in integration tests.</summary>
internal static class AuthFlow
{
    public const string Password = "Passw0rd!";

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
        return database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
    }

    /// <summary>Forces an account into a given lifecycle state, directly in the database.</summary>
    public static void SetAccountState(SimfApiFactory factory, string email, AccountState state)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        user.AccountState = state;
        database.SaveChanges();
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
