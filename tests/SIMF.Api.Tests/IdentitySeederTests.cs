using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for <see cref="IdentitySeeder"/>.</summary>
public sealed class IdentitySeederTests : IClassFixture<SimfApiFactory>
{
    private const string SuperAdminEmail = "superadmin@simf.test";

    private readonly SimfApiFactory _factory;

    public IdentitySeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task SeedAsync_creates_the_super_admin()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IdentitySeeder>().SeedAsync();

        var admin = await services.GetRequiredService<UserManager<SimfUser>>()
            .FindByEmailAsync(SuperAdminEmail);

        Assert.NotNull(admin);
        Assert.Equal(AccountState.Approved, admin!.AccountState);
        Assert.True(admin.PasswordChangeRequired);
        Assert.True(admin.EmailConfirmed);
    }

    [Fact]
    public async Task SeedAsync_provisions_the_super_admin_TOTP_secret()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var admin = await userManager.FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.GetTwoFactorEnabledAsync(admin!));
        Assert.Equal("JBSWY3DPEHPK3PXP", await userManager.GetAuthenticatorKeyAsync(admin!));
    }

    [Fact]
    public async Task SeedAsync_keeps_a_super_admin_2FA_disable_across_a_reseed()
    {
        // D-390 — a super-admin whose 2FA an operator deliberately disabled must
        // stay disabled after a restart (the seeder used to force it back on
        // every boot). A real disable also wipes the active authenticator key
        // (TotpEnrollmentService.DisableAsync), and the seeder must NOT resurrect
        // it while 2FA is off — re-pinning runs only when 2FA is enabled.
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        // ASP.NET Identity's authenticator-key token coordinates (the same ones
        // the seeder uses); GetAuthenticatorKeyAsync reads this exact token.
        const string keyProvider = "[AspNetUserStore]";
        const string keyName = "AuthenticatorKey";

        await seeder.SeedAsync();
        var admin = await userManager.FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
        var originalKey = await userManager.GetAuthenticatorKeyAsync(admin!);

        try
        {
            // Mimic a deliberate disable: 2FA off + active authenticator key gone.
            await userManager.SetTwoFactorEnabledAsync(admin!, false);
            await userManager.RemoveAuthenticationTokenAsync(admin!, keyProvider, keyName);

            // The next boot re-runs the seeder — it must leave the disable alone.
            await seeder.SeedAsync();

            var reloaded = await userManager.FindByEmailAsync(SuperAdminEmail);
            Assert.NotNull(reloaded);
            Assert.False(
                await userManager.GetTwoFactorEnabledAsync(reloaded!),
                "a super-admin 2FA disable must survive a re-seed (D-390)");
            Assert.Null(await userManager.GetAuthenticatorKeyAsync(reloaded!));
        }
        finally
        {
            // Restore the shared-fixture admin to the state the other tests in
            // this class expect (2FA on, with its provisioned key).
            var restore = await userManager.FindByEmailAsync(SuperAdminEmail);
            if (restore is not null)
            {
                if (originalKey is not null)
                {
                    await userManager.SetAuthenticationTokenAsync(
                        restore, keyProvider, keyName, originalKey);
                }
                await userManager.SetTwoFactorEnabledAsync(restore, true);
            }
        }
    }

    [Fact]
    public async Task SeedAsync_writes_a_SuperAdminSeeded_audit_entry()
    {
        using var scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Contains(
            database.OperationLog,
            entry => entry.EventType == AuditEvents.SuperAdminSeeded);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>()
            .FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task SeedAsync_seeds_the_registration_baseline_lookups_and_core_content()
    {
        // D-377 — the profile save REQUIRES interests + an organisation, so a
        // fresh environment must boot with both populated, plus the app's
        // terms/about content blocks. Double-run also proves idempotency
        // (the counts must not grow on the second pass).
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var interestCount = database.Interests.Count();
        var organisationCount = database.Organisations.Count();

        Assert.True(interestCount > 0, "baseline interests must be seeded");
        Assert.True(organisationCount > 0, "baseline organisations must be seeded");
        Assert.Contains(database.ContentBlocks, b => b.Key == "terms" && b.IsActive);
        Assert.Contains(database.ContentBlocks, b => b.Key == "about" && b.IsActive);
        // The "Other — not listed" catch-all keeps a visitor whose organisation
        // is missing from being blocked.
        Assert.Contains(database.Organisations, o => o.Name == "Other — not listed");

        await seeder.SeedAsync();
        Assert.Equal(interestCount, database.Interests.Count());
        Assert.Equal(organisationCount, database.Organisations.Count());
    }

    [Fact]
    public async Task SeedAsync_seeds_the_VVIP_and_VIP_visitor_tiers()
    {
        // V-1 (D-429) — the dedicated VIP registration page + the موج (Mawj)
        // welcome-message export rely on the VVIP and VIP audience
        // profile-types existing on a clean install. Both must be visitor-side
        // (IsForVisitor=true) so they flow through the standard visitor
        // approval queue, and the seed must stay idempotent.
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();

        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var vvip = database.ProfileTypes.SingleOrDefault(profileType => profileType.Name == "VVIP");
        var vip = database.ProfileTypes.SingleOrDefault(profileType => profileType.Name == "VIP");

        Assert.NotNull(vvip);
        Assert.True(vvip!.IsForVisitor, "VVIP must be a visitor-side tier");
        Assert.NotNull(vip);
        Assert.True(vip!.IsForVisitor, "VIP must be a visitor-side tier");
    }

    [Fact]
    public async Task SeedAsync_seeds_the_full_demo_account_matrix()
    {
        // D-585 — one demo account per user type / profile type so every role is
        // testable from a fresh DB. Admin → Administrator role, no profile;
        // visitor/partner → an Approved profile with a minted QR badge. The
        // second run proves idempotency (no duplicate accounts / profiles).
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        await seeder.SeedAsync();

        // The extra CP admin — Administrator role, no visitor profile.
        var admin = await users.FindByEmailAsync("admin@simf.local");
        Assert.NotNull(admin);
        Assert.Equal(UserType.Admin, admin!.UserType);
        Assert.True(await users.IsInRoleAsync(admin, "Administrator"));

        // A visitor — Approved, with an Approved profile carrying a QR badge
        // under the "Normal" profile type.
        var visitor = await users.FindByEmailAsync("visitor@simf.local");
        Assert.NotNull(visitor);
        Assert.Equal(UserType.Visitor, visitor!.UserType);
        Assert.Equal(AccountState.Approved, visitor.AccountState);
        var visitorProfile = database.UserProfiles.SingleOrDefault(p => p.UserId == visitor.Id);
        Assert.NotNull(visitorProfile);
        Assert.False(
            string.IsNullOrEmpty(visitorProfile!.QrId),
            "an Approved demo profile carries a QR badge");
        var normalType = database.ProfileTypes.Single(t => t.Id == visitorProfile.ProfileTypeId);
        Assert.Equal("Normal", normalType.Name);

        // A partner staff account resolves to the Staff app role via its profile type.
        var staff = await users.FindByEmailAsync("staff@simf.local");
        Assert.NotNull(staff);
        var staffProfile = database.UserProfiles.Single(p => p.UserId == staff!.Id);
        var staffType = database.ProfileTypes.Single(t => t.Id == staffProfile.ProfileTypeId);
        Assert.Equal(MobileAppRole.Staff, staffType.MobileAppRole);

        // All nine demo emails exist.
        foreach (var email in new[]
        {
            "admin@simf.local", "vvip@simf.local", "vip@simf.local", "visitor@simf.local",
            "staff@simf.local", "moderator@simf.local", "exhibitor@simf.local",
            "media@simf.local", "sponsor@simf.local",
        })
        {
            Assert.NotNull(await users.FindByEmailAsync(email));
        }

        // Idempotent — a second seed adds no duplicate demo profiles.
        var demoProfileCount = database.UserProfiles.Count(p => p.NationalId!.StartsWith("100000000"));
        await seeder.SeedAsync();
        Assert.Equal(
            demoProfileCount,
            database.UserProfiles.Count(p => p.NationalId!.StartsWith("100000000")));
    }
}
