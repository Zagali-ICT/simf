using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Seeds the bootstrap super-administrator account and the Administrator role,
/// and assigns the one to the other. Idempotent — running it again is a no-op
/// (decision D4, SIMF-FDS-001 Amendment A.4 and A.5; SIMF-RPM-001 section 5.1).
/// </summary>
public sealed class IdentitySeeder(
    IUserAccountRepository accounts,
    RoleManager<SimfRole> roleManager,
    SimfIdentityDbContext dbContext,
    IOptions<SuperAdminOptions> options,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<IdentitySeeder> logger)
{
    private const string AdministratorRole = AppRoles.Administrator;

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

        // P7 — seed the single CP RBAC role (Administrator). The P4-era
        // Staff / Scientific / Security roles were removed by the P7 rework
        // — they live in the ProfileTypes lookup now, not in AspNetRoles.
        foreach (var role in AppRoles.CpRoles)
        {
            await EnsureRoleAsync(role);
        }

        var admin = await accounts.FindByEmailAsync(settings.Email)
            ?? await CreateSuperAdminAsync(settings, cancellationToken);
        if (admin is null)
        {
            return;
        }

        if (!await accounts.IsInRoleAsync(admin, AdministratorRole))
        {
            await accounts.AddToRoleAsync(admin, AdministratorRole).EnsureSuccessAsync();
        }

        // P7 — every seeded admin must end up with UserType = Admin. This
        // also catches a super-admin row that was migrated up from a
        // pre-P7 database where the column did not exist.
        if (admin.UserType != UserType.Admin)
        {
            admin.UserType = UserType.Admin;
            await accounts.UpdateAsync(admin).EnsureSuccessAsync();
        }

        // P7 — seed the initial ProfileTypes set so the create / pending
        // pages have non-empty pickers from first boot. The final v1 set
        // is open item OI-6 against SIMF-FDS-002 v2.0 — the owner picks
        // the full list (VVIP / VIP / Gold / Staff / Exhibitor / Sponsor
        // / Media / ...); this seed ships one row per UserType so the
        // pickers render.
        await EnsureProfileTypeAsync(
            "Visitor — General", "زائر — عام", "#3B82F6",
            UserType.Visitor, cancellationToken);
        await EnsureProfileTypeAsync(
            "Other — Staff", "أخرى — فريق", "#10B981",
            UserType.Other, cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new SimfRole
            {
                Name = roleName,
                IsBaseline = true,
            });
        }
    }

    /// <summary>P7 — idempotent ProfileTypes seed (lookup by Name + UserType).</summary>
    private async Task EnsureProfileTypeAsync(
        string name,
        string nameArabic,
        string pageColor,
        UserType userType,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ProfileTypes
            .AnyAsync(profileType =>
                profileType.UserType == userType && profileType.Name == name,
                cancellationToken);
        if (exists) { return; }

        dbContext.ProfileTypes.Add(new ProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            PageColor = pageColor,
            UserType = userType,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SimfUser?> CreateSuperAdminAsync(
        SuperAdminOptions settings,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var admin = new SimfUser
        {
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            DisplayName = "Super Administrator",
            AccountState = AccountState.Approved,
            // P7 — the seeded super-admin is the only Admin-typed row at first
            // boot; the data migration in 20260524_AddUserTypeAndProfileType
            // already sets this for the pre-P7 super-admin, but we also set
            // it here so a brand-new install on a clean DB lands correctly.
            UserType = UserType.Admin,
            PasswordChangeRequired = true,
            CreatedAt = now,
        };

        var result = await accounts.CreateAsync(admin, settings.TempPassword);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Super-admin seed failed: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(settings.TotpSecret))
        {
            await accounts.SetAuthenticationTokenAsync( admin, AuthenticatorKeyProvider, AuthenticatorKeyTokenName, settings.TotpSecret).EnsureSuccessAsync();
            await accounts.SetTwoFactorEnabledAsync(admin, true).EnsureSuccessAsync();
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.SuperAdminSeeded,
                Outcome = AuditOutcome.Success,
                SubjectEmail = settings.Email,
                SubjectUserId = admin.Id,
            },
            cancellationToken);

        logger.LogInformation("Super-admin account seeded: {Email}", settings.Email);
        return admin;
    }
}
