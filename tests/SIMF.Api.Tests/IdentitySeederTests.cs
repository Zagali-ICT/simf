using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common;
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
    public async Task SeedAsync_derives_IsAppRegisterable_from_the_mobile_app_role()
    {
        // D-725 (owner item 1) — the seeder derives the app-sign-up-picker
        // visibility from MobileAppRole: the CP-only operational types (Staff,
        // Moderator) ship HIDDEN, everything else stays registerable. This is
        // the actual hide mechanism (the same rule the D-725 migration data
        // step applies), so it needs a direct assertion — and it guards the
        // critical invariant that the audience "Normal" type (the single type a
        // self-registering visitor is locked to, C5/D-371) is NEVER hidden,
        // which would silently break all mobile self-registration.
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();

        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        UserProfileType Type(string name) =>
            database.ProfileTypes.Single(profileType => profileType.Name == name);

        // Hidden from the app picker — CP-only operational types.
        Assert.False(Type("Staff").IsAppRegisterable, "Staff must be CP-only");
        Assert.False(Type("Moderator").IsAppRegisterable, "Moderator must be CP-only");

        // Registerable — audience tiers (must include Normal, the locked
        // self-registration type) + the non-operational / self-serviceable
        // partner types the owner did not name.
        Assert.True(Type("Normal").IsAppRegisterable, "Normal must stay registerable");
        Assert.True(Type("VVIP").IsAppRegisterable);
        Assert.True(Type("VIP").IsAppRegisterable);
        Assert.True(Type("Media").IsAppRegisterable);
        Assert.True(Type("Sponsor").IsAppRegisterable);
        Assert.True(Type("Exhibitor").IsAppRegisterable);
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

    [Fact]
    public async Task SeedAsync_makes_every_demo_account_profile_complete()
    {
        // BUG-022 regression — the Moderator and Exhibitor demo accounts could NEVER
        // be used in the app: EnsureDemoVisitorInterestsAsync only linked interests
        // for visitor@ / vip@ / vvip@ / staff@, and IsProfileCompleteAsync demands
        // >= 1 interest, so those accounts stayed profileComplete=false no matter
        // what the tester uploaded. The same pass now also seeds the ID document and
        // the face photo (avatar) every demo profile needs, so ALL eight
        // profile-carrying demo accounts are usable straight after a fresh seed.
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profiles = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IUserProfileService>();

        await seeder.SeedAsync();

        string[] demoProfileEmails =
        [
            "vvip@simf.local", "vip@simf.local", "visitor@simf.local", "staff@simf.local",
            "moderator@simf.local", "exhibitor@simf.local", "media@simf.local",
            "sponsor@simf.local",
        ];

        foreach (var email in demoProfileEmails)
        {
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(
                string.IsNullOrEmpty(user!.AvatarRelativePath),
                $"{email} must carry a seeded face photo");

            var profile = await database.UserProfiles
                .Include(p => p.Interests)
                .SingleAsync(p => p.UserId == user.Id);
            Assert.False(
                string.IsNullOrEmpty(profile.IdImageRelativePath),
                $"{email} must carry a seeded ID document");
            Assert.NotEmpty(profile.Interests);

            Assert.True(
                await profiles.IsProfileCompleteAsync(user.Id),
                $"{email} must be profileComplete out of the box");
        }

        // Idempotent — a re-seed uploads nothing new (the pointers stay put).
        var pointersBefore = await database.UserProfiles
            .Where(p => p.NationalId!.StartsWith("100000000"))
            .Select(p => p.IdImageRelativePath)
            .ToListAsync();
        await seeder.SeedAsync();
        var pointersAfter = await database.UserProfiles
            .Where(p => p.NationalId!.StartsWith("100000000"))
            .Select(p => p.IdImageRelativePath)
            .ToListAsync();
        Assert.Equal(pointersBefore, pointersAfter);
    }

    [Fact]
    public async Task SeedAsync_repairs_a_demo_image_whose_bytes_have_gone()
    {
        // Regression — EnsureDemoAccountAssetsAsync was documented "self-healing" but
        // skipped any account whose pointer was merely non-empty. A non-empty pointer
        // proves something was uploaded ONCE, not that it is still there, so the two
        // ways a store loses bytes underneath a healthy-looking pointer — the storage
        // root moves / the working folder is cleaned (bytes gone, row intact), and a
        // database is restored past its file store (row gone too) — both produced a
        // pointer no re-seed could ever repair. That is the "can't connect store/file"
        // 404 seen after a deployment reset. Both shapes are exercised here.
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();

        await seeder.SeedAsync();

        var user = await users.FindByEmailAsync("visitor@simf.local");
        Assert.NotNull(user);
        var profile = await database.UserProfiles.SingleAsync(p => p.UserId == user!.Id);

        var avatarBefore = user!.AvatarRelativePath;
        var idDocumentBefore = profile.IdImageRelativePath;
        Assert.False(string.IsNullOrEmpty(avatarBefore));
        Assert.False(string.IsNullOrEmpty(idDocumentBefore));

        // Shape 1 — the avatar's bytes vanish, its row survives (a moved root).
        var avatarKey = await database.StoredFiles.AsNoTracking()
            .Where(f => f.Id == Guid.Parse(avatarBefore!))
            .Select(f => f.StorageKey)
            .SingleAsync();
        await storage.DeleteAsync(avatarKey!);

        // Shape 2 — the ID document's row vanishes too (a restore past the store).
        var orphanedId = Guid.NewGuid();
        profile.IdImageRelativePath = orphanedId.ToString();
        await database.SaveChangesAsync();

        // The pre-condition the old guard could not see: both pointers still look
        // perfectly healthy — non-empty, well-formed — and both resolve to nothing.
        Assert.False(string.IsNullOrEmpty(user.AvatarRelativePath));
        Assert.False(await files.ContentExistsAsync(Guid.Parse(avatarBefore!)));
        Assert.False(await files.ContentExistsAsync(orphanedId));

        await seeder.SeedAsync();

        // Both are re-uploaded and re-pointed, and the new content is really there.
        var repairedUser = await users.FindByEmailAsync("visitor@simf.local");
        var repairedProfile = await database.UserProfiles
            .AsNoTracking().SingleAsync(p => p.UserId == user.Id);

        Assert.NotEqual(avatarBefore, repairedUser!.AvatarRelativePath);
        Assert.NotEqual(orphanedId.ToString(), repairedProfile.IdImageRelativePath);
        Assert.True(
            await files.ContentExistsAsync(Guid.Parse(repairedUser.AvatarRelativePath!)),
            "the re-seeded avatar must resolve to bytes that exist");
        Assert.True(
            await files.ContentExistsAsync(Guid.Parse(repairedProfile.IdImageRelativePath!)),
            "the re-seeded ID document must resolve to bytes that exist");

        // Still idempotent: a healthy pointer is left alone, so repair never becomes
        // a re-upload on every restart.
        await seeder.SeedAsync();
        var afterThirdSeed = await database.UserProfiles
            .AsNoTracking().SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(repairedProfile.IdImageRelativePath, afterThirdSeed.IdImageRelativePath);
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
