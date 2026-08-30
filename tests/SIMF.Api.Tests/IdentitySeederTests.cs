using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for <see cref="IdentitySeeder"/>, which since
/// 2026-08-30 bootstraps IDENTITY and nothing else - the Control Panel roles,
/// the permission catalogue and the single super-administrator.
///
/// <para>The lookups, CMS blocks and AI prompts it used to write moved to the
/// SQL seed lane and are covered by <see cref="SqlContentSeederTests"/>; the
/// demo <c>@simf.local</c> matrix moved to the test fixture and is covered by
/// <see cref="DemoAccountSeederTests"/>.</para></summary>
[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class IdentitySeederTests : IClassFixture<SimfApiFactory>
{
    private const string SuperAdminEmail = "superadmin@simf.test";

    private readonly SimfApiFactory _factory;

    public IdentitySeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    // The permission seed is ADD-ONLY, so deleting a code from PermissionCatalog
    // leaves its row (and any custom grants) behind in every already-seeded
    // database; only RetiredPermissionCodes clears it. Nothing pinned that
    // before, so Editions.Close was removed from the catalogue and survived in
    // the database until this was noticed by querying it.

    [Fact]
    public async Task SeedAsync_retires_a_permission_removed_from_the_catalogue()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var identityDb = services.GetRequiredService<SimfIdentityDbContext>();

        // Stand in for an older database that still carries the retired code.
        const string retired = "Editions.Close";
        if (!await identityDb.Permissions.AnyAsync(p => p.Code == retired))
        {
            identityDb.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = retired,
            });
            await identityDb.SaveChangesAsync();
        }

        await services.GetRequiredService<IdentitySeeder>().SeedAsync();

        Assert.False(
            await identityDb.Permissions.AnyAsync(p => p.Code == retired),
            $"{retired} was removed from PermissionCatalog, so the seeder must "
            + "retire its row; add it to IdentitySeeder.RetiredPermissionCodes.");
    }

    [Fact]
    public async Task SeedAsync_leaves_no_permission_row_the_catalogue_does_not_define()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var identityDb = services.GetRequiredService<SimfIdentityDbContext>();

        await services.GetRequiredService<IdentitySeeder>().SeedAsync();

        var catalogue = PermissionCatalog.All
            .Select(definition => definition.Code)
            .ToHashSet(StringComparer.Ordinal);
        var orphans = await identityDb.Permissions
            .Select(permission => permission.Code)
            .ToListAsync();

        // A code in the database that the catalogue no longer defines gates
        // nothing, yet still reads as a real authority to anyone querying the
        // table directly.
        Assert.DoesNotContain(orphans, code => !catalogue.Contains(code));
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
    public async Task SeedAsync_seeds_the_security_and_scientific_team_roles_with_their_baseline_grants()
    {
        // D-752 — the two new CP team roles (SecurityTeam / ScientificCommittee)
        // must auto-seed as baseline roles (EnsureRoleAsync loops AppRoles.CpRoles)
        // AND receive exactly the baseline permission codes the catalogue assigns
        // them. This proves the whole grant path end-to-end at the DB layer, not
        // just the in-memory catalogue (PermissionCatalogBaselineTests covers that).
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        await seeder.SeedAsync();

        var security = await roleManager.FindByNameAsync(AppRoles.SecurityTeam);
        var scientific = await roleManager.FindByNameAsync(AppRoles.ScientificCommittee);
        Assert.NotNull(security);
        Assert.True(security!.IsBaseline, "SecurityTeam is a built-in CP role");
        Assert.NotNull(scientific);
        Assert.True(scientific!.IsBaseline, "ScientificCommittee is a built-in CP role");

        async Task<HashSet<string>> GrantedCodesAsync(Guid roleId) =>
            (await (from rolePermission in identityDb.RolePermissions
                    join permission in identityDb.Permissions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == roleId
                    select permission.Code).ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

        HashSet<string> BaselineFor(string role) =>
            PermissionCatalog.All
                .Where(def => def.BaselineRoles.Contains(role))
                .Select(def => def.Code)
                .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(BaselineFor(AppRoles.SecurityTeam), await GrantedCodesAsync(security.Id));
        Assert.Equal(BaselineFor(AppRoles.ScientificCommittee), await GrantedCodesAsync(scientific.Id));

        // A sane spot-check that the catalogue baselines are not empty (guards a
        // future refactor that accidentally clears the team grant lists).
        Assert.Contains(PermissionCatalog.Gates.Manage, await GrantedCodesAsync(security.Id));
        Assert.Contains(PermissionCatalog.Sessions.View, await GrantedCodesAsync(scientific.Id));
    }
}
