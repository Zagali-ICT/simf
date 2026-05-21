using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Tests;

/// <summary>Shared helpers that drive the full authentication flow in integration tests.</summary>
internal static class AuthFlow
{
    public const string Password = "Passw0rd!";

    /// <summary>Signs a brand-new visitor up and verifies the email; returns the address.</summary>
    public static async Task<string> RegisterVerifiedVisitorAsync(
        HttpClient client,
        SimfApiFactory factory)
    {
        var email = $"flow-{Guid.NewGuid():N}@simf.test";

        await client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        await client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(factory, email, AccountCodePurpose.EmailVerification),
            });
        return email;
    }

    /// <summary>Signs a brand-new visitor in fully and returns the issued token pair.</summary>
    public static async Task<AuthTokens> SignInVisitorAsync(HttpClient client, SimfApiFactory factory)
    {
        var email = await RegisterVerifiedVisitorAsync(client, factory);

        var signIn = await client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = Password });
        var challenge = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
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
