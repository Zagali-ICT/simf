// Tests: SIMF.Api.Tests/IdentitySeederTests.cs (super-admin, TOTP, audit,
//        idempotency, permission catalogue + retirement,
//        2FA-disable-persists-across-reseed);
//        SIMF.Api.Tests/DemoAccountSeedGateTests.cs (this seeder creates NO
//        demo account, in any environment - the demo fixture lives in the test
//        project now);
//        SIMF.Api.Tests/SuperAdminSeedFailureTests.cs (a
//        policy-violating temp password throws in Production, logs-and-skips
//        in Development);
//        SIMF.Api.Tests/SuperAdminDuplicateSeedTests.cs (granting the
//        Administrator wildcard while other accounts already hold it is
//        audited and names them; re-seeding the same address audits nothing);
//        SIMF.Api.Tests/SeedRaceGuardTests.cs (the concurrent first-boot
//        tolerance every insert below now saves through)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Seeding;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// The identity bootstrap, and nothing else: the Control Panel roles, the whole
/// page-and-action permission catalogue, and the single super-administrator
/// account the roles are granted to. Idempotent - running it again is a no-op.
///
/// <para><b>Everything else it used to seed now lives in the SQL lane</b>
/// (<c>docs/migrations/2026/*.sql</c>, one location, one runner:
/// <c>Run_All_App_Seeds.sql</c>). That is an owner rule, not a preference: the
/// profile-type / interest / organisation lookups moved to
/// <c>SIMF_App_Lookups.sql</c>, the CMS blocks to
/// <c>SIMF_App_ContentBlocks.sql</c> and the default prompt catalogue to
/// <c>SIMF_App_AiPrompts.sql</c>. The demo <c>@simf.local</c> account matrix was
/// deleted outright - a fixture has no business running inside production
/// startup - and the integration suite creates its own in
/// <c>tests/SIMF.Api.Tests/DemoAccountSeeder.cs</c>.</para>
///
/// <para>The permission catalogue deliberately stays here. It is generated from
/// <see cref="PermissionCatalog"/>, which is the source of truth four test
/// suites compare the database against; expressed as hand-written SQL, every new
/// Control Panel page would need an INSERT that nothing checks.</para>
/// </summary>
public sealed class IdentitySeeder(
    IUserAccountRepository accounts,
    RoleManager<SimfRole> roleManager,
    SimfIdentityDbContext dbContext,
    IOptions<SuperAdminOptions> options,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
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

        // Seed the single CP RBAC role (Administrator). The earlier
        // Staff / Scientific / Security roles are gone — they live in the
        // ProfileTypes lookup now, not in AspNetRoles.
        foreach (var role in AppRoles.CpRoles)
        {
            await EnsureRoleAsync(role);
        }

        // Seed the full page-and-action permission catalogue from
        // PermissionCatalog. Idempotent by Code, so it is
        // safe on every boot. Baseline non-Administrator roles get their
        // seeded grants from PermissionDef.BaselineRoles (GateOperator → the
        // gate operator pair; PublicRelations → the invitation + VIP set).
        // Administrator is never granted per-code: it carries the wildcard
        // permission ("*") minted into its token and so holds every
        // permission implicitly. The six codes that predate the catalogue
        // (the gate triad, the PR/VIP triad) keep their exact strings and grants.
        await SeedPermissionCatalogAsync(cancellationToken);

        var admin = await accounts.FindByEmailAsync(settings.Email, cancellationToken)
            ?? await CreateSuperAdminAsync(settings, cancellationToken);
        if (admin is null)
        {
            return;
        }

        // Everything below hangs off the ROLE GRANT rather than off "the account
        // did not exist", because the grant is the moment the wildcard is handed
        // out and it is reached by two different routes: a changed
        // SuperAdmin:Email creates a second account, and a SuperAdmin:Email
        // pointed at an existing ordinary user promotes that one. Both end with
        // more than one account holding `perm:*`; keying on "created" would only
        // have seen the first.
        //
        // Snapshotted BEFORE the grant so the account being granted is not in its
        // own list, and reported AFTER it succeeds so the audit trail never claims
        // a privilege change that did not happen.
        var alreadyAdministrators = await accounts.IsInRoleAsync(admin, AdministratorRole, cancellationToken)
            ? []
            : await OtherAdministratorEmailsAsync(admin.Id, cancellationToken);

        if (!await accounts.IsInRoleAsync(admin, AdministratorRole, cancellationToken))
        {
            // Reported only when THIS instance performed the grant. On a
            // concurrent first boot the losing instance finds the role already
            // granted, and auditing the same privilege change from every node
            // would put one event in the trail per running instance.
            if (await TryGrantRoleAsync(admin, AdministratorRole, cancellationToken))
            {
                await ReportAdditionalAdministratorAsync(
                    settings.Email, alreadyAdministrators, cancellationToken);
            }
        }

        // Every seeded admin must end up with UserType = Admin. This
        // also catches a super-admin row that was migrated up from an
        // older database where the column did not exist.
        if (admin.UserType != UserType.Admin)
        {
            admin.UserType = UserType.Admin;
            await accounts.UpdateAsync(admin).EnsureSuccessAsync();
        }

        // Keep the configured TOTP secret in sync on an
        // EXISTING admin row, but NEVER force two-factor back on. The seeder
        // once re-enabled 2FA on every boot so the super-admin always carried
        // the second factor — but that meant an operator who deliberately
        // disabled the super-admin's 2FA found it switched back on after the
        // next restart. The disabled choice must survive a
        // restart. The self-heal therefore runs ONLY while 2FA is enabled —
        // when it is on, the active authenticator key is compared to config and
        // re-applied if it drifted (the original "TOTP not working" fix; the
        // appsettings value stays authoritative). When 2FA is off the seeder
        // leaves the row untouched: disabling 2FA wipes the active key
        // (TotpEnrollmentService.DisableAsync), so re-pinning it here would both
        // resurrect an orphan secret and mutate the row on every boot. 2FA is
        // still enabled once, at first creation, in CreateSuperAdminAsync.
        if (admin.TwoFactorEnabled && !string.IsNullOrWhiteSpace(settings.TotpSecret))
        {
            var activeSecret = await accounts.GetAuthenticationTokenAsync(
                admin, AuthenticatorKeyProvider, AuthenticatorKeyTokenName, cancellationToken);
            if (!string.Equals(activeSecret, settings.TotpSecret, StringComparison.Ordinal))
            {
                await accounts.SetAuthenticationTokenAsync(
                    admin, AuthenticatorKeyProvider, AuthenticatorKeyTokenName,
                    settings.TotpSecret, cancellationToken).EnsureSuccessAsync();
                logger.LogInformation(
                    "Super-admin TOTP secret re-applied from configuration for {Email}.",
                    settings.Email);
            }
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        try
        {
            await roleManager.CreateAsync(new SimfRole
            {
                Name = roleName,
                IsBaseline = true,
            });
        }
        catch (DbUpdateException exception) when (SeedRaceGuard.IsUniqueIndexViolation(exception))
        {
            // A concurrent first boot: another instance created the same role
            // between the existence check above and this insert, and the unique
            // index on the normalised name rejected ours. The role exists, which
            // is all this method promises, so the losing node detaches the
            // refused row and keeps booting. Detaching matters more here than the
            // log line: a rejected role left tracked as Added would be re-sent by
            // the permission-catalogue save a few lines later and fail it too.
            SeedRaceGuard.DetachAddedEntries(dbContext);
            logger.LogInformation(
                "Role '{Role}' seed lost the first-boot race — another instance created it.",
                roleName);
        }
    }

    /// <summary>
    /// Grants a role, tolerating a concurrent first boot. Returns true when THIS
    /// instance performed the grant and false when another instance had already
    /// done so, so a caller with an audit entry to write can tell the two apart.
    ///
    /// <para>A lost race surfaces two different ways depending on how the two
    /// instances interleave. If the winner commits after our caller's
    /// <c>IsInRoleAsync</c> check but before our insert, the composite primary
    /// key on the user-role table rejects it and Identity throws. If the winner
    /// commits slightly earlier, Identity's own duplicate check catches it first
    /// and returns a failed result instead. Both mean the same thing, and the
    /// second one is why this re-reads the role membership rather than matching
    /// on an Identity error code: the end state is the only thing that decides
    /// whether the seed step succeeded, and a genuine failure still throws.</para>
    /// </summary>
    private async Task<bool> TryGrantRoleAsync(
        SimfUser user, string role, CancellationToken cancellationToken)
    {
        UserOperationResult result;
        try
        {
            result = await accounts.AddToRoleAsync(user, role, cancellationToken);
        }
        catch (DbUpdateException exception) when (SeedRaceGuard.IsUniqueIndexViolation(exception))
        {
            SeedRaceGuard.DetachAddedEntries(dbContext);
            // Identity does not only insert the grant row: it stamps a fresh
            // concurrency value on the user first, so the refused save leaves
            // this row tracked as Modified with a stamp the database never
            // accepted. Reloading it is what keeps the boot alive — otherwise the
            // NEXT save on this context (creating the next demo account is one)
            // flushes that stale UPDATE and dies on a concurrency failure, which
            // carries no store error and so is not a race this guard can forgive.
            await dbContext.Entry(user).ReloadAsync(cancellationToken);
            logger.LogInformation(
                "Role grant '{Role}' lost the first-boot race — another instance granted it.",
                role);
            return false;
        }

        if (result.Succeeded)
        {
            return true;
        }

        if (await accounts.IsInRoleAsync(user, role, cancellationToken))
        {
            logger.LogInformation(
                "Role grant '{Role}' lost the first-boot race — another instance granted it.",
                role);
            return false;
        }

        throw new InvalidOperationException(
            $"Seeding could not grant the '{role}' role: "
            + string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}")));
    }

    /// <summary>Idempotent seed of the whole Permission catalogue plus
    /// its baseline role grants. Batched: read the existing permissions, grants
    /// and roles ONCE, diff the catalogue in memory, and persist any additions
    /// in a single SaveChanges — instead of a SELECT-per-code (plus an AnyAsync
    /// per grant) on every boot. Still idempotent by Code and by
    /// (RoleId, PermissionId). Safe to re-run on every startup.</summary>
    private async Task SeedPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        // Drop permissions removed from the catalogue before re-seeding.
        await RetireRemovedPermissionsAsync(cancellationToken);

        var permissionsByCode = await dbContext.Permissions
            .ToDictionaryAsync(p => p.Code, cancellationToken);
        var existingGrants = (await dbContext.RolePermissions
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        // Resolve each distinct baseline role once, through the same RoleManager
        // normalisation the per-item path used — the catalogue references only a
        // handful of roles, so this is a few lookups, not one per grant.
        var rolesByName = new Dictionary<string, SimfRole>();
        foreach (var roleName in PermissionCatalog.All
            .SelectMany(def => def.BaselineRoles).Distinct())
        {
            if (await roleManager.FindByNameAsync(roleName) is { } role)
            {
                rolesByName[roleName] = role;
            }
        }

        foreach (var def in PermissionCatalog.All)
        {
            if (!permissionsByCode.TryGetValue(def.Code, out var permission))
            {
                permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                };
                dbContext.Permissions.Add(permission);
                permissionsByCode[def.Code] = permission;
            }

            foreach (var roleName in def.BaselineRoles)
            {
                if (!rolesByName.TryGetValue(roleName, out var role)) { continue; }
                if (existingGrants.Add((role.Id, permission.Id)))
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                    });
                }
            }
        }

        // Tolerates a concurrent first boot. Both the unique index on
        // Permission.Code and the composite key on the role-grant table reject
        // whichever instance arrives second, and the rows it was rejected for
        // are the rows this step exists to guarantee.
        await dbContext.SaveToleratingFirstBootRaceAsync(
            logger, "Permission catalogue seed", cancellationToken);
    }

    /// <summary>#6/#17 (owner 2026-07-20) — codes retired from
    /// <see cref="PermissionCatalog"/>. The catalogue seed is add-only, so an
    /// already-seeded database keeps orphan <c>Permission</c> rows (and any custom
    /// <c>RolePermission</c> grants) until they are removed here. Bookings.Approve /
    /// Bookings.Reject went with the booking approval step; Editions.Close was
    /// seeded but never gated anything, and a year is only ever closed by opening
    /// the next one.</summary>
    private static readonly string[] RetiredPermissionCodes =
    [
        "Bookings.Approve",
        "Bookings.Reject",
        "Editions.Close",
    ];

    /// <summary>Idempotent cleanup of retired permissions: delete any
    /// role grants of the retired codes, then the permission rows themselves. A
    /// no-op once they are gone, so it is safe to run on every boot.</summary>
    private async Task RetireRemovedPermissionsAsync(CancellationToken cancellationToken)
    {
        var stale = await dbContext.Permissions
            .Where(p => RetiredPermissionCodes.Contains(p.Code))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return;
        }

        var staleIds = stale.Select(p => p.Id).ToList();
        var grants = await dbContext.RolePermissions
            .Where(rp => staleIds.Contains(rp.PermissionId))
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(grants);
        dbContext.Permissions.RemoveRange(stale);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The concurrent-first-boot case for a cleanup step rather than an
            // insert one, so it does not go through the shared unique-index
            // guard: a lost delete race reports the wrong number of affected
            // rows, not a duplicate key. This batch is nothing but DELETEs of
            // rows read moments earlier, so the only way the store can report
            // fewer rows than expected is that another instance deleted the same
            // retired permissions first — which is the end state this method
            // exists to reach. A genuine delete failure still arrives as a plain
            // DbUpdateException carrying a store error and still propagates.
            //
            // The pending deletes are detached for the same reason a failed
            // insert is: left tracked they would be re-sent by the catalogue save
            // that follows and fail it on rows that are already gone.
            var pendingDeletes = dbContext.ChangeTracker.Entries()
                .Where(entry => entry.State == EntityState.Deleted)
                .ToList();
            foreach (var entry in pendingDeletes)
            {
                entry.State = EntityState.Detached;
            }
            logger.LogInformation(
                "Retired-permission cleanup lost the first-boot race — another instance removed them.");
            return;
        }

        logger.LogInformation(
            "Retired {PermissionCount} removed permission(s) and {GrantCount} grant(s): {Codes}",
            stale.Count, grants.Count, string.Join(", ", stale.Select(p => p.Code)));
    }

    /// <summary>
    /// The e-mail of every account already holding the Administrator role, other
    /// than <paramref name="excludedUserId"/>. Ordered so the log line and the
    /// audit entry are stable between boots and can be diffed.
    /// </summary>
    private async Task<List<string>> OtherAdministratorEmailsAsync(
        Guid excludedUserId,
        CancellationToken cancellationToken) =>
        await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == AdministratorRole && user.Id != excludedUserId
            orderby user.Email
            select user.Email!)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Reports that seeding granted the Administrator wildcard to an account while
    /// other accounts already held it.
    ///
    /// <para>Reached two ways, both of which end with more than one account holding
    /// <c>perm:*</c>: pointing <c>SuperAdmin:Email</c> at a new address creates a
    /// second super-admin and leaves the first signing in with its old credentials,
    /// and pointing it at an existing ordinary user promotes that user instead.
    /// Neither said anything in the boot path before this.</para>
    ///
    /// <para>It goes to the audit trail and not only the log because a startup line
    /// scrolls away, while a second unattended super-admin is exactly what a
    /// security review has to be able to find after the fact. Filed as
    /// <see cref="AuditOutcome.Failure"/> so it appears in the report a reviewer
    /// actually runs — the seed step succeeded, but it left the system in a state
    /// nobody asked for.</para>
    ///
    /// <para>It reports; it does not refuse to boot. An Administrator can also be
    /// created legitimately in the Control Panel, so their presence is not proof of
    /// a mistake, and failing startup on a guess would take the API down for a
    /// condition that may be intentional. Resolving it needs an operator decision
    /// either way — see <c>docs/migrations/2026/DEPLOY.md</c>.</para>
    /// </summary>
    private async Task ReportAdditionalAdministratorAsync(
        string configuredEmail,
        IReadOnlyList<string> existingAdministrators,
        CancellationToken cancellationToken)
    {
        if (existingAdministrators.Count == 0)
        {
            return;
        }

        // Capped because Detail is a single column and an estate with many admins
        // would otherwise truncate mid-address; the count is always exact, so a
        // reader can tell the list was shortened rather than being silently misled.
        const int MaxListed = 10;
        var listed = string.Join(", ", existingAdministrators.Take(MaxListed));
        var others = existingAdministrators.Count > MaxListed
            ? $"{listed}, … (+{existingAdministrators.Count - MaxListed} more)"
            : listed;

        logger.LogWarning(
            "Seeding granted the Administrator role to {Configured}, but {Count} other "
            + "account(s) already hold it ({Others}). More than one account now carries "
            + "the perm:* wildcard, and the others keep their existing credentials. If "
            + "SuperAdmin:Email was changed deliberately, migrate or remove the "
            + "superseded row; see docs/migrations/2026/DEPLOY.md.",
            configuredEmail, existingAdministrators.Count, others);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.SuperAdminDuplicateSeeded,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = configuredEmail,
                Detail =
                    $"{existingAdministrators.Count} other account(s) already hold the "
                    + $"Administrator wildcard: {others}",
            },
            cancellationToken);
    }

    private async Task<SimfUser?> CreateSuperAdminAsync(
        SuperAdminOptions settings,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.SimfNow();
        var admin = new SimfUser
        {
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            DisplayName = "Super Administrator",
            AccountState = AccountState.Approved,
            // The seeded super-admin is the only Admin-typed row at first
            // boot; the data migration in 20260524_AddUserTypeAndProfileType
            // already sets this for an existing super-admin, but we also set
            // it here so a brand-new install on a clean DB lands correctly.
            UserType = UserType.Admin,
            // The seed credential is normally forced to
            // rotate on first CP login. Config-driven (SuperAdmin:
            // PasswordChangeRequired, default true) so a dev / test box can opt
            // out; keep it true for the production / NCA handover.
            PasswordChangeRequired = settings.PasswordChangeRequired,
            CreatedAt = now,
        };

        UserOperationResult result;
        try
        {
            result = await accounts.CreateAsync(admin, settings.TempPassword, cancellationToken);
        }
        catch (DbUpdateException exception) when (SeedRaceGuard.IsUniqueIndexViolation(exception))
        {
            // A concurrent first boot: another instance inserted the same
            // super-admin between Identity's duplicate-address check and this
            // write, and the unique index on the normalised user name rejected
            // ours. The refused row is detached so it cannot poison the next
            // Identity save, and the winner's row is returned — the rest of
            // SeedAsync needs an account to hang the role grant and the TOTP
            // sync off, and refusing to boot over an account that now exists is
            // exactly the failure this tolerance is here to prevent.
            SeedRaceGuard.DetachAddedEntries(dbContext);
            return await ResolveSuperAdminAfterLostRaceAsync(settings.Email, cancellationToken);
        }

        if (!result.Succeeded)
        {
            var reasons = string.Join("; ", result.Errors.Select(error => error.Description));
            logger.LogError("Super-admin seed failed: {Errors}", reasons);

            // The other half of the same race. When the winner commits early
            // enough, Identity's duplicate-address check catches it before the
            // insert and this is a failed result rather than a thrown one. Re-read
            // the address before treating the failure as a failed deployment:
            // a row that exists now is the winner's, while the failure this
            // branch was written for — a temp password the policy rejects —
            // leaves no row behind and still falls through to the throw below.
            if (await ResolveSuperAdminAfterLostRaceAsync(settings.Email, cancellationToken)
                is { } winner)
            {
                return winner;
            }

            // This used to log and return null,
            // and the caller returned too, so the API booted normally with NO
            // super-admin and a Control Panel nobody could sign into — discovered
            // only when someone tried. Program.cs does fail fast in Production, but
            // only for the exact committed DEFAULT temp password; a CUSTOM password
            // that merely violates the policy sails past that guard into this path.
            //
            // In Production a bootstrap account that cannot be created is a failed
            // deployment, so fail the boot and name the policy rule that broke, so
            // the operator can correct the configured value instead of guessing.
            // Outside Production the log-and-skip stands: a developer on a
            // half-configured box should still be able to start the app.
            if (hostEnvironment.IsProduction())
            {
                throw new InvalidOperationException(
                    "The super-administrator account could not be seeded, so the "
                    + "Control Panel would have no way in. The configured "
                    + "SuperAdmin:TempPassword was rejected: " + reasons
                    + ". Set a compliant value and restart.");
            }
            return null;
        }

        if (!string.IsNullOrWhiteSpace(settings.TotpSecret))
        {
            await accounts.SetAuthenticationTokenAsync(admin, AuthenticatorKeyProvider, AuthenticatorKeyTokenName, settings.TotpSecret).EnsureSuccessAsync();
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

    /// <summary>
    /// The super-admin row after our own attempt to create it failed, or null
    /// when the address still does not exist.
    ///
    /// <para>The caller only ever reaches the creation path because
    /// <see cref="SeedAsync"/> looked the address up and found nothing, so an
    /// account that exists by the time we get here was written by another
    /// instance seeding the same fresh database. Returning it lets the losing
    /// instance finish booting against the winner's row; the winner owns the
    /// TOTP provisioning and the seeded-account audit entry, so neither is
    /// repeated here.</para>
    ///
    /// <para>Null is the honest answer for every other failure — a temp password
    /// the policy rejects leaves no row behind — and the caller still fails the
    /// boot in Production on that answer.</para>
    /// </summary>
    private async Task<SimfUser?> ResolveSuperAdminAfterLostRaceAsync(
        string email, CancellationToken cancellationToken)
    {
        var winner = await accounts.FindByEmailAsync(email, cancellationToken);
        if (winner is null)
        {
            return null;
        }

        logger.LogInformation(
            "Super-admin seed lost the first-boot race — another instance created {Email}.",
            email);
        return winner;
    }
}
