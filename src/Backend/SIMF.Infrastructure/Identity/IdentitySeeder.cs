using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Seeds the bootstrap super-administrator account. Idempotent — running it
/// again once the account exists is a no-op (decision D4, SIMF-FDS-001
/// Amendment A.4 and A.5).
/// </summary>
public sealed class IdentitySeeder(
    UserManager<SimfUser> userManager,
    IOptions<SuperAdminOptions> options,
    TimeProvider timeProvider,
    ILogger<IdentitySeeder> logger)
{
    // ASP.NET Core Identity's internal token coordinates for the TOTP
    // authenticator key, so a pre-provisioned secret is recognised by
    // UserManager.GetAuthenticatorKeyAsync.
    private const string AuthenticatorKeyProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Email) ||
            string.IsNullOrWhiteSpace(settings.TempPassword))
        {
            logger.LogWarning(
                "Super-admin seed skipped — SuperAdmin:Email or SuperAdmin:TempPassword is not configured.");
            return;
        }

        if (await userManager.FindByEmailAsync(settings.Email) is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var admin = new SimfUser
        {
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            DisplayName = "Super Administrator",
            AccountState = AccountState.Approved,
            PasswordChangeRequired = true,
            CreatedAt = now,
        };

        var result = await userManager.CreateAsync(admin, settings.TempPassword);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Super-admin seed failed: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.TotpSecret))
        {
            await userManager.SetAuthenticationTokenAsync(
                admin, AuthenticatorKeyProvider, AuthenticatorKeyTokenName, settings.TotpSecret);
            await userManager.SetTwoFactorEnabledAsync(admin, true);
        }

        logger.LogInformation("Super-admin account seeded: {Email}", settings.Email);
    }
}
