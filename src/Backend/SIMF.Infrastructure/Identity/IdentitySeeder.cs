// Tests: SIMF.Api.Tests/IdentitySeederTests.cs (super-admin, TOTP, audit,
//        idempotency, baseline lookups + core content,
//        2FA-disable-persists-across-reseed, demo-account matrix,
//        demo-image repair when the bytes or the row have gone);
//        SIMF.Api.Tests/DemoAccountSeedGateTests.cs (demo seed is
//        a no-op outside Development / with Seed:EnableDemoAccounts off);
//        SIMF.Api.Tests/SuperAdminSeedFailureTests.cs (a
//        policy-violating temp password throws in Production, logs-and-skips
//        in Development);
//        SIMF.Api.Tests/SuperAdminDuplicateSeedTests.cs (granting the
//        Administrator wildcard while other accounts already hold it is
//        audited and names them; re-seeding the same address audits nothing)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Organization;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Seeds the bootstrap super-administrator account and the Administrator role,
/// and assigns the one to the other. Idempotent — running it again is a no-op.
/// </summary>
public sealed class IdentitySeeder(
    IUserAccountRepository accounts,
    RoleManager<SimfRole> roleManager,
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext,
    IOptions<SuperAdminOptions> options,
    IOptions<DemoSeedOptions> demoOptions,
    IQrIdMinter qrIdMinter,
    IFileService fileService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<IdentitySeeder> logger)
{
    // Saudi Arabia is the seeded default nationality (Country.Id, the
    // ISO-3166 numeric code, seeded via CountryConfiguration.HasData).
    private const int SaudiArabiaCountryId = 682;

    // The real edition dates and the single source of truth for the seeded
    // OrganizationProfile forum dates. The row is CP-editable after seeding; the
    // seeder only writes these when the row still carries the stale placeholder
    // (2026-01-01..04-30) a migration's InsertData baked in, so a CP edit is
    // never overwritten on restart. The hero MetaDate label is derived from the
    // same dates via the shared EventDateRange formatter (no hardcoded literal).
    private static readonly DateOnly EventStartDate = new(2026, 11, 23);
    private static readonly DateOnly EventEndDate = new(2026, 11, 25);
    private static readonly DateTime StalePlaceholderStart =
        new(2026, 1, 1, 0, 0, 0);
    private static readonly DateTime StalePlaceholderEnd =
        new(2026, 4, 30, 0, 0, 0);

    private const string AdministratorRole = AppRoles.Administrator;

    // ASP.NET Core Identity's internal token coordinates for the TOTP
    // authenticator key, so a pre-provisioned secret is recognised by
    // UserManager.GetAuthenticatorKeyAsync.
    private const string AuthenticatorKeyProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";

    /// <summary>The demo account matrix: one account per user type /
    /// profile type so every role is testable from a fresh database.
    /// <c>ProfileType == null</c> → an Admin account (Administrator role, no
    /// profile). This is the SINGLE source of truth for the demo set —
    /// the interest and asset passes below read it too, so a new demo account can
    /// no longer be added here and silently forgotten there (which is exactly how
    /// moderator@ and exhibitor@ ended up with no interests and therefore a
    /// permanently incomplete profile the app refused to let past sign-in).</summary>
    private static readonly (string Email, string DisplayName, string EnName, string ArName,
        UserType UserType, string? ProfileType, string NationalId)[] DemoAccounts =
    [
        ("admin@simf.local",     "Demo Admin",     "Demo Admin",     "مدير تجريبي",         UserType.Admin,   null,        "1000000001"),
        ("vvip@simf.local",      "Demo VVIP",      "Demo VVIP",      "شخصية بالغة الأهمية", UserType.Visitor, "VVIP",      "1000000002"),
        ("vip@simf.local",       "Demo VIP",       "Demo VIP",       "شخصية مهمة",          UserType.Visitor, "VIP",       "1000000003"),
        ("visitor@simf.local",   "Demo Visitor",   "Demo Visitor",   "زائر تجريبي",         UserType.Visitor, "Normal",    "1000000004"),
        ("staff@simf.local",     "Demo Staff",     "Demo Staff",     "موظف تجريبي",         UserType.Visitor, "Staff",     "1000000005"),
        ("moderator@simf.local", "Demo Moderator", "Demo Moderator", "منسّق تجريبي",        UserType.Visitor, "Moderator", "1000000006"),
        ("exhibitor@simf.local", "Demo Exhibitor", "Demo Exhibitor", "عارض تجريبي",         UserType.Visitor, "Exhibitor", "1000000007"),
        ("media@simf.local",     "Demo Media",     "Demo Media",     "إعلامي تجريبي",       UserType.Visitor, "Media",     "1000000008"),
        ("sponsor@simf.local",   "Demo Sponsor",   "Demo Sponsor",   "راعٍ تجريبي",         UserType.Visitor, "Sponsor",   "1000000009"),
    ];

    /// <summary>The demo accounts that carry a <see cref="UserProfile"/>
    /// (everything except the CP-only Admin), i.e. the ones the completeness rule
    /// applies to.</summary>
    private static IEnumerable<string> DemoProfileEmails =>
        DemoAccounts.Where(demo => demo.ProfileType is not null).Select(demo => demo.Email);

    /// <summary>A small placeholder portrait (64×64 PNG) stored as the
    /// demo accounts' face photo, and a placeholder ID card (96×64 PNG) stored as
    /// their identity document. Real bytes through the real upload pipeline, so a
    /// demo account satisfies the male-face + ID-document halves of
    /// <c>UserProfileService.IsProfileCompleteAsync</c> out of the box.</summary>
    private static readonly byte[] DemoAvatarPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAwklEQVR42u3Yyw2EMAxF0dcGNVDF9F/BbFlTBNNARiEx+JMruYB7JCRia9s/qUcAAAAAAAAAAAAAADTne5z5AL/o1kQH/El/iKH3620Ncqk3NMir3sogx3oTw/KAyfp5AwAAngCT+kkDnxAAADwl0gPSv0Yr7AMVNrIKO3GFq0SRuxCnRQAAuE7zI1vmMZd4oTE8ScwwFKR+2KA49WMGhaofMCha/V2DAtbfMihmfb9hDYBLfacBQHyAY32PAQCA1QEXDhmFwqhDWYMAAAAASUVORK5CYII=");

    private static readonly byte[] DemoIdDocumentPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAGAAAABACAIAAABqVuVZAAAAmElEQVR42u3cMQ2AQBBE0bNBkIAKSoTgDwUYwA0JCSUooNzA8JKv4DWz11zrhlEPNQSAAJUB7cepO0CAAAEClAHUT/M7AwQIECBAgAABAgQI0JeBvMUAAQIECBCgWKBl3fICBAgQoBQgKwYIECBALunynQIECBCgCCArBggQIECAAAECBAgQIECAAAECBOjHQPLzAiBAxUAX6SqBUHBIRtAAAAAASUVORK5CYII=");

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
            await accounts.AddToRoleAsync(admin, AdministratorRole, cancellationToken).EnsureSuccessAsync();
            await ReportAdditionalAdministratorAsync(
                settings.Email, alreadyAdministrators, cancellationToken);
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

        // The original seed names redundantly prefixed the UserType
        // ("Visitor — General", "Other — Staff") even though UserType is a
        // separate column on the same row. Rename the rows in place on any
        // DB that still carries the old names — the CP grid now surfaces
        // UserType as its own column, so the prefix is noise. The
        // rename runs before the EnsureProfileTypeAsync calls below so the
        // ensure step is a true no-op afterwards.
        await RenameProfileTypeIfPresentAsync(
            "Visitor — General", "General", "زائر — عام", "عام",
            UserType.Visitor, cancellationToken);
        // Legacy "Other — Staff" rows are under UserType.Visitor
        // after the data migration; rename + audience-vs-partner state
        // is preserved on the row's IsVisitor flag (false for Staff).
        await RenameProfileTypeIfPresentAsync(
            "Other — Staff", "Staff", "أخرى — فريق", "فريق",
            UserType.Visitor, cancellationToken);

        // The owner fixed the visitor self-registration type's
        // name as "Normal" (عادي); rename any DB still carrying the older
        // "General" row in place.
        await RenameProfileTypeIfPresentAsync(
            "General", "Normal", "عام", "عادي",
            UserType.Visitor, cancellationToken);

        // Seed the initial ProfileTypes set so the create / pending
        // pages have non-empty pickers from first boot. Every seeded row
        // sits under UserType.Visitor; the partner-side
        // ones (Staff / Media / Sponsor) carry IsVisitor=false so the
        // CP "Others" approval queue finds them. "Normal" is
        // the single audience-side type a visitor self-registers under.
        await EnsureProfileTypeAsync(
            "Normal", "عادي", "#3B82F6",
            isVisitor: true, MobileAppRole.None, cancellationToken);
        // Staff is the canonical operational partner-side profile
        // type; the default mobile-app role is Staff (can perform gate
        // operations, look up attendees, print badges). Admins seed the
        // remaining operational types (Volunteer → Staff,
        // Programme Coordinator / Operations Lead → Moderator,
        // Sponsor / Speaker → None; Exhibitor → Exhibitor) via the
        // CP runtime.
        await EnsureProfileTypeAsync(
            "Staff", "فريق", "#10B981",
            isVisitor: false, MobileAppRole.Staff, cancellationToken);
        // Seed the canonical Moderator partner profile type alongside
        // Staff (MobileAppRole.Moderator = Staff + content/user moderation), so a
        // moderator app account is creatable out of the box. The seeder note above
        // ("Programme Coordinator / Operations Lead → Moderator") still lets admins
        // add further Moderator-mapped types at runtime; this is the canonical one.
        await EnsureProfileTypeAsync(
            "Moderator", "منسّق", "#6366F1", // indigo — distinct from Staff green
            isVisitor: false, MobileAppRole.Moderator, cancellationToken);
        // The partner-tier seed also ships Media
        // and Sponsor as canonical operational types. Both default to
        // MobileAppRole.None — they are display categories, not
        // operational authority (a sponsor's representative is not
        // automatically a gate operator). Distinct PageColors so the
        // badges are visually unmistakable.
        await EnsureProfileTypeAsync(
            "Media", "إعلامي", "#F59E0B", // amber
            isVisitor: false, MobileAppRole.None, cancellationToken);
        await EnsureProfileTypeAsync(
            "Sponsor", "راعي", "#8B5CF6", // purple
            isVisitor: false, MobileAppRole.None, cancellationToken);
        // The canonical exhibitor (العارض) partner type. Unlike the
        // display-only Media / Sponsor types, an exhibitor carries the
        // operational Exhibitor app role so the lead-capture tools (scan a
        // visitor's QR + "My Visitors") gate to it. Booth-officer accounts are
        // assigned this type so they resolve to AppRole.exhibitor in the app.
        await EnsureProfileTypeAsync(
            "Exhibitor", "عارض", "#0891B2", // cyan
            isVisitor: false, MobileAppRole.Exhibitor, cancellationToken);
        // The VVIP / VIP audience tiers used by the dedicated VIP
        // registration page + the موج (Mawj) welcome-message export. Both
        // are audience-side (IsForVisitor=true) so they appear in the
        // visitor picker and flow through the standard visitor approval
        // queue; no special mobile-app authority (MobileAppRole.None).
        // "Normal" stays the slot-0 default; these are added alongside.
        // Distinct PageColors so the tier is unmistakable on the badge.
        // Distinct Arabic names: VIP keeps the established
        // "كبار الشخصيات" convention; VVIP is the higher "بالغة الأهمية"
        // tier, so the two cards never read identically in an Arabic UI.
        await EnsureProfileTypeAsync(
            "VVIP", "شخصيات بالغة الأهمية", "#B91C1C", // deep red
            isVisitor: true, MobileAppRole.None, cancellationToken);
        await EnsureProfileTypeAsync(
            "VIP", "كبار الشخصيات", "#0E7490", // deep teal
            isVisitor: true, MobileAppRole.None, cancellationToken);

        // Mark the VVIP + VIP audience tiers as the VIP tier. Despite its name
        // the flag no longer has anything to do with meetings: it decides who may
        // self-reserve a VIP-tier SEAT (SeatReservationService.IsVipVisitorAsync)
        // and it is what the app receives as UserProfileResponse.IsVip. Speaker
        // meetings moved to the per-user UserProfile.AllowsSpeakerMeeting flag.
        // It defaults false for every other type; flip these two after seeding
        // (idempotent — runs each boot). This flag, rather than the former
        // "profile-type Name contains 'VIP'" substring test, is the source of
        // truth, so a future type whose name merely embeds those letters is not
        // wrongly treated as VIP. It is seeder-owned: no admin API or Control
        // Panel path writes it.
        await appDbContext.ProfileTypes
            .Where(profileType => profileType.Name == "VVIP" || profileType.Name == "VIP")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(profileType => profileType.IsVipTier, true),
                cancellationToken);

        // Seed one demo user account per user type / profile type
        // (an extra Admin + a VVIP/VIP/Normal visitor + a Staff/Moderator/
        // Exhibitor/Media/Sponsor partner), so every role is testable from a
        // fresh DB. Runs AFTER the profile types above so the name lookup
        // resolves. Security: these accounts — including
        // an Administrator-role admin@simf.local — must NEVER exist in
        // production, so the whole seed is gated to the Development environment
        // or an explicit Seed:EnableDemoAccounts opt-in (default false).
        // Production is clean by construction.
        if (hostEnvironment.IsDevelopment() || demoOptions.Value.EnableDemoAccounts)
        {
            await EnsureDemoAccountsAsync(admin.Id, cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Demo-account seed skipped — not Development and Seed:EnableDemoAccounts is false.");
        }

        // Seed the cybersecurity
        // policy content blocks the Flutter "سياسات وضوابط الأمن
        // السيبراني" screen reads. Idempotent: only writes the row when
        // missing, matches the existing EnsureProfileTypeAsync pattern.
        await EnsureCybersecurityPolicyContentAsync(admin.Id, cancellationToken);

        // Set the CP-editable forum dates to the real edition
        // (2026-11-23..25), correcting the stale placeholder the migration
        // seeded, so every surface that reads OrganizationProfile renders the real
        // range. Idempotent + admin-edit-safe (only rewrites the known placeholder).
        await EnsureOrganizationProfileEventDatesAsync(cancellationToken);

        // Seed the public marketing landing's hero CMS text blocks so the
        // Website's /content/site proxy can serve them and the CP CMS editor
        // can manage them. Idempotent — same insert-when-absent shape.
        await EnsureLandingHeroContentAsync(admin.Id, cancellationToken);

        // Seed the landing's editorial sections below the hero — About, the
        // global-landscape stats strip, the Pillars header and the Goals
        // block — so the same /content/site proxy + CP CMS editor drive them
        // instead of the page's hardcoded copy. Idempotent, additive data.
        await EnsureLandingSectionsContentAsync(admin.Id, cancellationToken);

        // Baseline lookups + core app content. Interests and the
        // organisation lookup are REQUIRED by the visitor profile save
        // (1–10 interests + an organisation pick), so an environment where
        // either table is empty silently makes registration impossible —
        // exactly what happened on the first production install (the rows
        // were entered by hand through the admin API). Seed only when the
        // table is completely empty: admins own the lists at runtime and a
        // deliberate deletion must never be re-added on the next boot.
        await EnsureBaselineInterestsAsync(admin.Id, cancellationToken);
        await EnsureBaselineOrganisationsAsync(admin.Id, cancellationToken);

        // Give the Approved demo accounts overlapping
        // interests so "قابل أشخاص مثلك" returns matches on a fresh DB. Runs after
        // the demo accounts (above) + the interest lookup (just now) are seeded.
        // App-DB-only, idempotent (skips a profile that already has interests).
        // This is also the first half of the completeness rule: every
        // demo profile needs ≥ 1 interest before the app treats it as complete.
        await EnsureDemoVisitorInterestsAsync(cancellationToken);

        // The second half: an ID document + a face photo for every demo
        // profile, so all eight demo accounts land on profileComplete=true and are
        // usable straight after a fresh seed. Idempotent (skips an account that
        // already carries the pointer); a no-op when the demo seed is gated off.
        await EnsureDemoAccountAssetsAsync(cancellationToken);

        // The app's terms + about content blocks (the Terms and About screens
        // render their empty states without them). Insert-when-absent, same
        // shape as the cyber/landing content seeds above.
        await EnsureCoreAppContentAsync(admin.Id, cancellationToken);

        // The forum's event CONTENT (org About/Vision/Mission/Themes +
        // social links, the demo speaker roster, sponsors + media partners, and
        // the past-edition archive) moved out of this seeder into the by-hand
        // SQL lane (docs/migrations/2026/*.sql). In
        // Development/Testing it is applied by SqlContentSeeder; in production it
        // is run by hand. The OrganizationProfile singleton row itself still
        // exists via EF HasData.

        // Seed the default AI prompt catalogue.
        // One prompt per feature; admin can edit at runtime via the CP.
        await EnsureDefaultAiPromptsAsync(admin.Id, cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);
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
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Retired {PermissionCount} removed permission(s) and {GrantCount} grant(s): {Codes}",
            stale.Count, grants.Count, string.Join(", ", stale.Select(p => p.Code)));
    }

    /// <summary>Idempotent rename. When a row with the old Name
    /// still exists for the given UserType, swap Name + NameArabic in
    /// place so the per-UserType uniqueness rule isn't violated by a
    /// duplicate "General" / "Staff" insert in the follow-up
    /// <see cref="EnsureProfileTypeAsync"/> call. Safe to re-run.</summary>
    private async Task RenameProfileTypeIfPresentAsync(
        string oldName,
        string newName,
        string oldNameArabic,
        string newNameArabic,
        UserType userType,
        CancellationToken cancellationToken)
    {
        var legacy = await appDbContext.ProfileTypes
            .SingleOrDefaultAsync(profileType =>
                profileType.Name == oldName,
                cancellationToken);
        if (legacy is null) { return; }

        // Bail out if the destination name is already taken on a different
        // row (e.g. the operator created their own "General" manually).
        // Leaving the legacy row alone is safer than colliding the unique
        // (UserType, Name) constraint.
        var collision = await appDbContext.ProfileTypes
            .AnyAsync(profileType =>
                profileType.Id != legacy.Id
                && profileType.Name == newName,
                cancellationToken);
        if (collision) { return; }

        legacy.Name = newName;
        if (string.Equals(legacy.NameArabic, oldNameArabic, StringComparison.Ordinal))
        {
            legacy.NameArabic = newNameArabic;
        }
        legacy.UpdatedAt = timeProvider.SimfNow();
        // ProfileType lives on App DB.
        await appDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Renamed seeded ProfileType '{OldName}' to '{NewName}' for {UserType}.",
            oldName, newName, userType);
    }

    /// <summary>Idempotent ProfileTypes seed (lookup by Name + UserType).
    /// The <paramref name="mobileAppRole"/> parameter lets seed
    /// rows ship with the right mobile-app authority out of the box.
    /// Every seeded profile type now goes under <c>UserType.Visitor</c>;
    /// the audience-vs-partner distinction is carried by
    /// <paramref name="isVisitor"/>.</summary>
    private async Task EnsureProfileTypeAsync(
        string name,
        string nameArabic,
        string pageColor,
        bool isVisitor,
        MobileAppRole mobileAppRole,
        CancellationToken cancellationToken)
    {
        var exists = await appDbContext.ProfileTypes
            .AnyAsync(profileType =>
                profileType.Name == name,
                cancellationToken);
        if (exists) { return; }

        appDbContext.ProfileTypes.Add(new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            PageColor = pageColor,
            IsForVisitor = isVisitor,
            MobileAppRole = mobileAppRole,
            // CP-only operational types (Staff,
            // Moderator) are hidden from the app sign-up picker; everything
            // else is self-registerable by default. Mirrors the
            // migration data step so a fresh-seeded DB matches a migrated one.
            IsAppRegisterable = mobileAppRole is not (MobileAppRole.Staff or MobileAppRole.Moderator),
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
            // Mirrors the migration's backfill so a fresh-seeded database
            // carries badge codes exactly as a migrated one does.
            Code = await ProfileTypeCodeAllocator.NextAsync(appDbContext, cancellationToken),
        });
        // ProfileType lives on App DB.
        await appDbContext.SaveChangesAsync(cancellationToken);
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

        var result = await accounts.CreateAsync(admin, settings.TempPassword);
        if (!result.Succeeded)
        {
            var reasons = string.Join("; ", result.Errors.Select(error => error.Description));
            logger.LogError("Super-admin seed failed: {Errors}", reasons);

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

    /// <summary>Seed one demo account per user type / profile type so
    /// every role is testable from a fresh database. Idempotent by email (an
    /// existing account is skipped). An Admin account carries the Administrator
    /// role and no profile; a visitor/partner account gets an <b>Approved</b>
    /// <see cref="UserProfile"/> (Saudi nationality) with a minted QR badge.
    /// <para><b>Security:</b> the caller in
    /// <see cref="SeedAsync"/> only invokes this in the Development environment
    /// or when <c>Seed:EnableDemoAccounts</c> is explicitly true, so these
    /// accounts never exist in production. The shared password comes from
    /// <see cref="DemoSeedOptions.DemoPassword"/> (no committed default — an
    /// empty value skips the seed here as a second backstop).</para></summary>
    private async Task EnsureDemoAccountsAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        var password = demoOptions.Value.DemoPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Demo-account seed skipped — Seed:DemoPassword is empty.");
            return;
        }

        var now = timeProvider.SimfNow();
        foreach (var demo in DemoAccounts)
        {
            if (await accounts.FindByEmailAsync(demo.Email) is not null)
            {
                continue; // idempotent — already seeded.
            }

            var user = new SimfUser
            {
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                DisplayName = demo.DisplayName,
                AccountState = AccountState.Approved,
                UserType = demo.UserType,
                PasswordChangeRequired = false,
                CreatedAt = now,
                StateChangedAt = now,
                StateChangedByUserId = actorUserId,
            };

            var createResult = await accounts.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Demo account {Email} could not be created: {Errors}",
                    demo.Email,
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
                continue;
            }

            if (demo.UserType == UserType.Admin)
            {
                // A CP admin — Administrator role, no visitor profile.
                await accounts.AddToRoleAsync(user, AdministratorRole).EnsureSuccessAsync();
                logger.LogInformation("Demo admin account seeded: {Email}", demo.Email);
                continue;
            }

            // A visitor / partner — needs an Approved profile + a QR badge.
            var profileType = await appDbContext.ProfileTypes
                .SingleOrDefaultAsync(profileType => profileType.Name == demo.ProfileType, cancellationToken);
            if (profileType is null)
            {
                logger.LogWarning(
                    "Demo account {Email}: profile type '{ProfileType}' not found — profile skipped.",
                    demo.Email, demo.ProfileType);
                continue;
            }

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = profileType.Id,
                Name = demo.EnName,
                NameArabic = demo.ArName,
                Gender = Gender.Male,
                NationalityId = SaudiArabiaCountryId,
                IsSaudi = true,
                NationalId = demo.NationalId,
                // A demo account is seeded ready to use, so its profile is
                // admitted outright — the QR minted just below only works for an
                // approved attendee, and a demo that cannot pass a gate would be
                // useless for exactly the walkthroughs it exists to support.
                AdmissionState = AccountState.Approved,
                CreatedAt = now,
            };
            // Approved accounts carry a QR badge.
            await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);
            appDbContext.UserProfiles.Add(profile);
            // UserProfile (with its QrId) lives on the App DB.
            await appDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Demo {ProfileType} account seeded: {Email}", demo.ProfileType, demo.Email);
        }
    }

    /// <summary>Seed the cybersecurity-policy content blocks the Flutter
    /// mobile app reads at <c>/api/v1/content/cyber.*</c>. Idempotent: each
    /// block is inserted only when its key is absent (the same shape
    /// EnsureProfileTypeAsync uses). The text is the approved copy
    /// verbatim (Arabic) + a paired English translation so the existing
    /// bilingual ContentBlock contract is respected.</summary>
    private async Task EnsureCybersecurityPolicyContentAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        // (Key, EN, AR) — matches the screen's layout:
        //  cyber.title              — the page heading
        //  cyber.intro              — the leading paragraph mentioning NCA
        //  cyber.pillar.01.title    — pillar headings
        //  cyber.pillar.01.body     — pillar bodies
        //  cyber.reference          — the references footer
        var seed = new[]
        {
            ("cyber.title",
             "Cybersecurity policies and controls",
             "سياسات وضوابط الأمن السيبراني"),
            ("cyber.intro",
             "The SIMF mobile application complies with the cybersecurity policies and controls issued by the National Cybersecurity Authority (NCA), based on the Essential Cybersecurity Controls (ECC – 1:2018) and the Critical Systems Cybersecurity Controls (CSCC – 1:2019).",
             "يلتزم تطبيق الملتقى البحري السعودي الدولي بسياسات وضوابط الأمن السيبراني الصادرة عن الهيئة الوطنية للأمن السيبراني (NCA)، استناداً إلى الضوابط الأساسية للأمن السيبراني (ECC – 1:2018) وضوابط الأمن السيبراني للأنظمة الحساسة (CSCC – 1:2019)."),
            ("cyber.pillar.01.title",
             "Personal data protection and privacy",
             "حماية البيانات الشخصية والخصوصية"),
            ("cyber.pillar.01.body",
             "Data is collected for specified purposes only and retained under approved policies.",
             "جمع البيانات لأغراض محددة فقط، وحفظها وفق الأنظمة المعتمدة"),
            ("cyber.pillar.02.title",
             "Encryption and communications protection",
             "التشفير وحماية الاتصالات"),
            ("cyber.pillar.02.body",
             "Data is encrypted in transit and at rest using approved standards.",
             "تشفير البيانات أثناء النقل والتخزين باستخدام معايير معتمدة"),
            ("cyber.pillar.03.title",
             "Access and authentication controls",
             "ضوابط الوصول والمصادقة"),
            ("cyber.pillar.03.body",
             "Multi-factor authentication and least-privilege are enforced.",
             "المصادقة متعددة العوامل ومبدأ أقل صلاحية لازمة"),
            ("cyber.pillar.04.title",
             "Security review and testing",
             "مراجعة واختبار الأمن"),
            ("cyber.pillar.04.body",
             "Penetration tests and vulnerability assessments before launch and on every update.",
             "اختبارات اختراق وتقييم ثغرات قبل الإطلاق وعند كل تحديث"),
            ("cyber.pillar.05.title",
             "Incident reporting and response",
             "الإبلاغ عن الحوادث والاستجابة"),
            ("cyber.pillar.05.body",
             "A documented reporting channel with a defined response time for handling incidents.",
             "قناة موثقة للإبلاغ وزمن استجابة محدد لمعالجة الحوادث"),
            ("cyber.reference",
             "References: National Cybersecurity Authority · ECC – 1:2018 · CSCC – 1:2019 · OWASP ASVS",
             "مرجعية: الهيئة الوطنية للأمن السيبراني · ECC – 1:2018 · CSCC – 1:2019 · OWASP ASVS"),
        };

        var now = timeProvider.SimfNow();
        var existingKeys = await appDbContext.ContentBlocks
            .Where(b => seed.Select(s => s.Item1).Contains(b.Key))
            .Select(b => b.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, en, ar) in seed)
        {
            if (existingKeys.Contains(key)) { continue; }
            appDbContext.ContentBlocks.Add(new SIMF.Domain.Cms.ContentBlock
            {
                Id = Guid.NewGuid(),
                Key = key,
                Content = en,
                ContentArabic = ar,
                IsActive = true,
                LastUpdatedByUserId = actorUserId,
                CreatedAt = now,
                LastUpdatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Cybersecurity policy content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

    /// <summary>Correct the singleton OrganizationProfile's forum dates to
    /// the real edition (<see cref="EventStartDate"/>..<see cref="EventEndDate"/>).
    /// The migration seeds the row with a stale placeholder (2026-01-01..04-30);
    /// this rewrites it in place so the app + Website read the real range. Idempotent
    /// and admin-edit-safe: it writes only when the row is null-dated or still carries
    /// the exact placeholder, so a CP edit survives every restart.</summary>
    private async Task EnsureOrganizationProfileEventDatesAsync(CancellationToken cancellationToken)
    {
        var profile = await appDbContext.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        var isUncorrected =
            (profile.EventStartDate is null && profile.EventEndDate is null)
            || (profile.EventStartDate == StalePlaceholderStart
                && profile.EventEndDate == StalePlaceholderEnd);
        if (!isUncorrected)
        {
            return;
        }

        profile.EventStartDate = ToLocalMidnight(EventStartDate);
        profile.EventEndDate = ToLocalMidnight(EventEndDate);
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "OrganizationProfile forum dates set to the real edition ({Start}..{End}).",
            EventStartDate, EventEndDate);
    }

    /// <summary>Midnight on the given Saudi calendar date. Was ToUtcMidnight and
    /// attached a +00:00 offset; stored values are Saudi-local now, so the name
    /// would have been a lie and the offset a three-hour shift.</summary>
    private static DateTime ToLocalMidnight(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue);

    /// <summary>Seed the public marketing landing's hero CMS text blocks
    /// (read by the Website's <c>/content/site</c> proxy and editable from the
    /// CP CMS editor). Keys are lowercase to match the CMS service's key
    /// normalisation; the Arabic values are the landing's current hardcoded
    /// defaults verbatim, with paired English translations. Idempotent: each
    /// block is inserted only when its key is absent.</summary>
    private async Task EnsureLandingHeroContentAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        // (Key, EN, AR) — the landing's data-cms="hero.*" bindings. Keys come
        // from the shared LandingHeroContentKeys so the seeder and the Website
        // proxy cannot drift on the exact key strings.
        var seed = new[]
        {
            (LandingHeroContentKeys.TitleStart,
             "The future of",
             "مستقبل أمن"),
            (LandingHeroContentKeys.TitleHighlight,
             "seabed security",
             "قاع البحار"),
            (LandingHeroContentKeys.TitleEnd,
             "and global supply chains",
             "وسلاسل الإمداد العالميّة"),
            (LandingHeroContentKeys.Tagline,
             "A global Saudi platform bringing leaders, decision-makers and experts together to shape the future of maritime security and protect vital corridors amid accelerating geopolitical and technological change.",
             "منصّة سعوديّة عالميّة تجمع القادة وصنّاع القرار والخبراء لاستشراف مستقبل الأمن البحري وحماية الممرّات الحيوية في ظل التحولّات الجيوسياسيّة والتقنيّة المتسارعة."),
            // Derived from the seeded event dates (not a literal), so the
            // hero label tracks OrganizationProfile.EventStartDate/EventEndDate.
            (LandingHeroContentKeys.MetaDate,
             EventDateRange.Format(EventStartDate, EventEndDate, arabic: false),
             EventDateRange.Format(EventStartDate, EventEndDate, arabic: true)),
            (LandingHeroContentKeys.MetaVenue,
             "Sofitel Riyadh Hotel & Convention Centre",
             "فندق ومركز مؤتمرات سوفيتيل الرياض"),
            (LandingHeroContentKeys.CtaSecondary,
             "Browse the programme",
             "تصفّح البرنامج"),
        };

        var now = timeProvider.SimfNow();
        var existingKeys = await appDbContext.ContentBlocks
            .Where(b => seed.Select(s => s.Item1).Contains(b.Key))
            .Select(b => b.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, en, ar) in seed)
        {
            if (existingKeys.Contains(key)) { continue; }
            appDbContext.ContentBlocks.Add(new SIMF.Domain.Cms.ContentBlock
            {
                Id = Guid.NewGuid(),
                Key = key,
                Content = en,
                ContentArabic = ar,
                IsActive = true,
                LastUpdatedByUserId = actorUserId,
                CreatedAt = now,
                LastUpdatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Landing hero content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

    /// <summary>Seed the landing's editorial sections below the hero — About,
    /// the global-landscape stats strip, the Pillars header and the Goals
    /// block — as CMS content blocks read by the Website's
    /// <c>/content/site</c> proxy and editable from the CP CMS editor. The
    /// Arabic values are the landing's current hardcoded defaults verbatim,
    /// with their paired English copy from the page's i18n dictionary. Keys
    /// come from the shared <see cref="LandingSectionContentKeys"/> so the
    /// seeder and the proxy cannot drift. Idempotent: each block is inserted
    /// only when its key is absent; additive data — no migration.</summary>
    private async Task EnsureLandingSectionsContentAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        // (Key, EN, AR) — mirrors the landing's data-cms="about.* / stats.* /
        // pillars.* / goals.*" bindings.
        var seed = new[]
        {
            // About.
            (LandingSectionContentKeys.AboutEyebrow,
             "About the Forum",
             "حول الملتقى"),
            (LandingSectionContentKeys.AboutHeading,
             "A Saudi global platform driving dialogue and cooperation on maritime security",
             "منصة سعودية عالمية لدعم الحوار والتعاون في قضايا الأمن البحري"),
            (LandingSectionContentKeys.AboutBody1,
             "The Saudi International Maritime Forum is a high-level event that brings together leaders, officials, and experts to share experience and build a shared global understanding of the future of maritime security amid accelerating geopolitical and technological change.",
             "الملتقى البحري السعودي الدولي حدث رفيع المستوى يجمع القادة والمسؤولين والخبراء لتبادل التجارب والخبرات، وتعزيز فهم عالمي مشترك لمستقبل الأمن البحري في ظل التحولات الجيوسياسية والتقنية المتسارعة."),
            (LandingSectionContentKeys.AboutBody2,
             "The Forum reflects the Kingdom of Saudi Arabia's strategic role in anchoring stability across the seas and supporting the resilience of the global economy through an integrated framework that protects the seabed and enhances the efficiency of energy and trade supply chains.",
             "يعكس الملتقى الدور الاستراتيجي للمملكة العربية السعودية في ترسيخ استقرار البحار ودعم استدامة الاقتصاد العالمي، عبر منظومة متكاملة لحماية قاع البحار ورفع كفاءة سلاسل إمداد الطاقة والتجارة."),

            // Global-landscape stats strip.
            (LandingSectionContentKeys.StatsEyebrow,
             "Global Landscape",
             "المشهد العالمي"),
            (LandingSectionContentKeys.StatsIntro,
             "The world is witnessing unprecedented shifts in maritime security. As threats to global supply chains escalate, seabed security emerges as an urgent international priority for stabilising the seas and sustaining the global economy.",
             "يشهد العالم تحولات غير مسبوقة في أمن البحار، ومع تصاعد التهديدات التي تطال سلاسل الإمداد العالمية، يبرز أمن قاع البحار كأولوية دولية ملحة لتعزيز استقرار البحار وضمان استدامة الاقتصاد العالمي."),
            (LandingSectionContentKeys.StatsHeading,
             "A progressive path tracking the shifts in global maritime security",
             "مسار متدرج يواكب تحولات الأمن البحري العالمي"),
            (LandingSectionContentKeys.StatsCount1, "500", "500"),
            (LandingSectionContentKeys.StatsLabel1,
             "Participating countries", "دولة مشاركة"),
            (LandingSectionContentKeys.StatsCount2, "220", "220"),
            (LandingSectionContentKeys.StatsLabel2,
             "Leaders & officials", "قائد ومسؤول"),
            (LandingSectionContentKeys.StatsCount3, "100", "100"),
            (LandingSectionContentKeys.StatsLabel3,
             "International speakers", "متحدث دولي"),
            (LandingSectionContentKeys.StatsCount4, "40", "40"),
            (LandingSectionContentKeys.StatsLabel4,
             "Sessions & dialogues", "جلسة وحوار"),

            // Pillars header.
            (LandingSectionContentKeys.PillarsEyebrow, "Key Pillars", "المحاور الرئيسية"),
            (LandingSectionContentKeys.PillarsHeading, "Key Pillars", "المحاور الرئيسية"),
            (LandingSectionContentKeys.PillarsBody,
             "Building a comprehensive strategic vision that addresses energy systems, trade, and the link between surface and depths through five core pillars that anchor maritime security and global economic stability.",
             "لصياغة رؤية استراتيجية شاملة تعالج منظومات الطاقة والتجارة والاتصال بين السطح والأعماق عبر خمسة محاور رئيسية تشكل ركائز الأمن البحري واستقرار الاقتصاد العالمي."),

            // Goals.
            (LandingSectionContentKeys.GoalsEyebrow, "Forum Goals", "أهداف الملتقى"),
            (LandingSectionContentKeys.GoalsHeading, "Ambitious Goals", "أهداف طموحة"),
            (LandingSectionContentKeys.GoalsBody,
             "Building an integrated maritime security framework that supports international efforts to protect the seabed and enhance supply-chain efficiency, contributing to global economic stability in alignment with Saudi Vision 2030.",
             "تعزيز منظومة أمن بحري متكاملة تدعم الجهود الدولية لحماية قاع البحار ورفع كفاءة سلاسل الإمداد، بما يسهم في استقرار الاقتصاد العالمي ويتّسق مع مستهدفات رؤية المملكة 2030."),
            (LandingSectionContentKeys.GoalsButton,
             "Browse all goals", "تصفّح الأهداف الكاملة"),
            (LandingSectionContentKeys.Goal1Title,
             "Strengthen regional and international maritime security",
             "تعزيز الأمن البحري الإقليمي والدولي"),
            (LandingSectionContentKeys.Goal1Body,
             "Unifying efforts to protect vital maritime corridors and ensure the stability of global navigation.",
             "توحيد الجهود لحماية الممرّات البحرية الحيويّة وضمان استقرار حركة الملاحة العالميّة."),
            (LandingSectionContentKeys.Goal2Title,
             "Protect subsea infrastructure",
             "حماية البنية التحتيّة تحت السطح"),
            (LandingSectionContentKeys.Goal2Body,
             "Safeguarding cables, energy lines, and pipelines that connect the global economy beneath the sea.",
             "صون الكابلات وخطوط الطاقة والأنابيب التي تربط الاقتصاد العالمي تحت قاع البحار."),
            (LandingSectionContentKeys.Goal3Title,
             "Enhance supply-chain efficiency",
             "رفع كفاءة سلاسل الإمداد"),
            (LandingSectionContentKeys.Goal3Body,
             "Modernising ports, corridors, and shipping systems to increase resilience and reduce risk.",
             "تطوير منظومات الموانئ والممرّات وأنظمة الشحن لرفع المرونة وتقليل المخاطر."),
            (LandingSectionContentKeys.Goal4Title,
             "Exchange knowledge and build capacity",
             "تبادل المعرفة وبناء القدرات"),
            (LandingSectionContentKeys.Goal4Body,
             "Expanding the knowledge platform between leaders and experts to develop national and international talent.",
             "توسيع منصّة المعرفة بين القادة والخبراء لصقل الكوادر الوطنيّة والدوليّة."),
            (LandingSectionContentKeys.Goal5Title,
             "Contribute to Vision 2030",
             "الإسهام في تحقيق رؤية 2030"),
            (LandingSectionContentKeys.Goal5Body,
             "Strengthening the Kingdom's position as a global hub for maritime security and the blue economy.",
             "تعزيز موقع المملكة قطبًا عالميًّا في الأمن البحري والاقتصاد الأزرق."),
        };

        var now = timeProvider.SimfNow();
        var existingKeys = await appDbContext.ContentBlocks
            .Where(b => seed.Select(s => s.Item1).Contains(b.Key))
            .Select(b => b.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, en, ar) in seed)
        {
            if (existingKeys.Contains(key)) { continue; }
            appDbContext.ContentBlocks.Add(new SIMF.Domain.Cms.ContentBlock
            {
                Id = Guid.NewGuid(),
                Key = key,
                Content = en,
                ContentArabic = ar,
                IsActive = true,
                LastUpdatedByUserId = actorUserId,
                CreatedAt = now,
                LastUpdatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Landing section content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

    /// <summary>Baseline interests for the visitor profile picker.
    /// The profile save REQUIRES 1–10 interests, so an empty table blocks
    /// registration outright. Seeds only when the table is empty (admins own
    /// the list at runtime; a deliberate deletion is never re-added).</summary>
    private async Task EnsureBaselineInterestsAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        if (await appDbContext.Interests.AnyAsync(cancellationToken)) { return; }

        var seed = new (string Name, string NameArabic, int Order)[]
        {
            ("Naval Defence Technologies", "تقنيات الدفاع البحري", 1),
            ("Maritime Security", "الأمن البحري", 2),
            ("Shipbuilding & Marine Industries", "بناء السفن والصناعات البحرية", 3),
            ("Ports & Maritime Logistics", "الموانئ والخدمات اللوجستية البحرية", 4),
            ("Hydrography & Marine Survey", "الهيدروغرافيا والمسح البحري", 5),
            ("Marine Environment & Sustainability", "البيئة البحرية والاستدامة", 6),
            ("Autonomous & Unmanned Systems", "الأنظمة ذاتية التشغيل وغير المأهولة", 7),
            ("Maritime Cybersecurity", "الأمن السيبراني البحري", 8),
            ("Investment & Local Content", "الاستثمار والمحتوى المحلي", 9),
            ("Research & Innovation", "البحث والابتكار", 10),
        };

        var now = timeProvider.SimfNow();
        foreach (var (name, nameArabic, order) in seed)
        {
            appDbContext.Interests.Add(new UserInterest
            {
                Id = Guid.NewGuid(),
                Name = name,
                NameArabic = nameArabic,
                DisplayOrder = order,
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Baseline interests seeded ({Count} rows; table was empty).",
            seed.Length);
    }

    /// <summary>Baseline organisation lookup for the profile's
    /// required الجهة pick. Includes an explicit
    /// "Other — not listed" row so a visitor whose organisation is missing
    /// is never blocked. Seeds only when the table is empty.</summary>
    private async Task EnsureBaselineOrganisationsAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        if (await appDbContext.Organisations.AnyAsync(cancellationToken)) { return; }

        var seed = new (string NameArabic, string? Name, string? Sector)[]
        {
            ("القوات البحرية الملكية السعودية", "Royal Saudi Naval Forces", "Government"),
            ("وزارة الدفاع", "Ministry of Defense", "Government"),
            ("الهيئة العامة للموانئ (موانئ)", "Saudi Ports Authority (Mawani)", "Government"),
            ("الشركة السعودية للصناعات العسكرية", "Saudi Arabian Military Industries (SAMI)", "Defence"),
            ("الشركة الوطنية السعودية للنقل البحري (البحري)", "Bahri", "Shipping & Logistics"),
            ("أرامكو السعودية", "Saudi Aramco", "Energy"),
            ("شركة الزامل أوفشور", "Zamil Offshore", "Marine Services"),
            ("جامعة الملك فهد للبترول والمعادن", "King Fahd University of Petroleum and Minerals", "Academia"),
            ("جامعة الملك عبدالله للعلوم والتقنية", "King Abdullah University of Science and Technology (KAUST)", "Academia"),
            ("أخرى — غير مدرجة", "Other — not listed", null),
        };

        var now = timeProvider.SimfNow();
        foreach (var (nameArabic, name, sector) in seed)
        {
            appDbContext.Organisations.Add(new SIMF.Domain.Organisations.Organisation
            {
                Id = Guid.NewGuid(),
                NameArabic = nameArabic,
                Name = name,
                Sector = sector,
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Baseline organisations seeded ({Count} rows; table was empty).",
            seed.Length);
    }

    /// <summary>The app's terms + about content blocks (the same
    /// bilingual copy first entered on production by hand). Insert-when-
    /// absent per key; admins edit at runtime via the CP Content Blocks
    /// page. One term per line — the app renders each line as one
    /// gold-bullet card (frame 505:1553).</summary>
    private async Task EnsureCoreAppContentAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        var termsEn = string.Join('\n',
            "These terms and conditions govern the use of the Saudi International Maritime Forum app and attendance at its events; by using the app you agree to them.",
            "Registration data must be accurate and match your official identity document; the forum administration may reject or cancel any incomplete or incorrect registration.",
            "Entry to the forum venue is by the personal QR code issued in the app after registration approval; it must not be shared with others.",
            "Bringing unlicensed photography or audio-recording equipment into the forum venue is prohibited.",
            "Visitors must follow all security and organisational instructions issued by the forum administration and security personnel across all facilities.",
            "Hazardous or legally prohibited materials are not allowed into the venue; bags and belongings are subject to security inspection.",
            "The organiser may photograph and film the events; by attending you consent to the use of such material for documentation and media purposes.",
            "Personal data is processed in accordance with the applicable laws of the Kingdom of Saudi Arabia and solely for the purposes of organising the forum.",
            "The forum administration may amend these terms, the event programme, or schedules when necessary; updates are announced through the app.");
        var termsAr = string.Join('\n',
            "تسري هذه الشروط والأحكام على استخدام تطبيق الملتقى الدولي البحري وعلى حضور فعالياته، وباستخدامك للتطبيق فإنك توافق عليها.",
            "يجب أن تكون بيانات التسجيل صحيحة ومطابقة للهوية الرسمية، ويحق لإدارة الملتقى رفض أو إلغاء أي تسجيل غير مكتمل أو غير صحيح.",
            "الدخول إلى مقر الملتقى يتم بواسطة رمز الاستجابة السريعة (QR) الشخصي الصادر عبر التطبيق بعد اعتماد التسجيل، ولا يجوز مشاركته مع الغير.",
            "يُمنع إدخال أي أجهزة تصوير أو تسجيل صوتي غير مرخصة إلى مقر الملتقى.",
            "يلتزم الزائر بالتعليمات الأمنية والتنظيمية الصادرة عن إدارة الملتقى وأفراد الأمن في جميع المرافق.",
            "يُمنع إدخال المواد الخطرة أو الممنوعة نظاماً إلى مقر الملتقى، وتخضع الحقائب والمقتنيات للتفتيش الأمني.",
            "قد تقوم الجهة المنظمة بالتصوير الفوتوغرافي والمرئي للفعاليات، وبحضورك فإنك توافق على استخدام هذه المواد لأغراض التوثيق والإعلام.",
            "تُعالج بياناتك الشخصية وفق الأنظمة المعمول بها في المملكة العربية السعودية ولأغراض تنظيم الملتقى فقط.",
            "يحق لإدارة الملتقى تعديل هذه الشروط أو برنامج الفعاليات أو المواعيد عند الاقتضاء، ويتم الإشعار بأي تحديث عبر التطبيق.");

        var aboutEn = string.Join('\n',
            "The Saudi International Maritime Forum is hosted by the Royal Saudi Naval Forces, bringing together decision-makers, experts, and leading companies of the maritime and defence sector from around the world.",
            "The forum aims to strengthen international cooperation, exchange expertise, and showcase the latest maritime technologies, supporting the goals of Saudi Vision 2030 in localising the defence and maritime industries.",
            "The programme includes panel sessions, workshops, an accompanying exhibition, and professional networking opportunities for participants and visitors.");
        var aboutAr = string.Join('\n',
            "الملتقى الدولي البحري حدث تستضيفه القوات البحرية الملكية السعودية، يجمع صنّاع القرار والخبراء والشركات الرائدة في القطاع البحري والدفاعي من مختلف دول العالم.",
            "يهدف الملتقى إلى تعزيز التعاون الدولي وتبادل الخبرات واستعراض أحدث التقنيات البحرية، بما يدعم مستهدفات رؤية المملكة 2030 في توطين الصناعات الدفاعية والبحرية.",
            "يتضمن برنامج الملتقى جلسات حوارية وورش عمل ومعرضاً مصاحباً وفرصاً للتواصل المهني بين المشاركين والزوار.");

        var seed = new[]
        {
            ("terms", termsEn, termsAr),
            ("about", aboutEn, aboutAr),
        };

        var now = timeProvider.SimfNow();
        var existingKeys = await appDbContext.ContentBlocks
            .Where(b => seed.Select(s => s.Item1).Contains(b.Key))
            .Select(b => b.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, en, ar) in seed)
        {
            if (existingKeys.Contains(key)) { continue; }
            appDbContext.ContentBlocks.Add(new SIMF.Domain.Cms.ContentBlock
            {
                Id = Guid.NewGuid(),
                Key = key,
                Content = en,
                ContentArabic = ar,
                IsActive = true,
                LastUpdatedByUserId = actorUserId,
                CreatedAt = now,
                LastUpdatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Core app content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

    /// <summary>Idempotently seeds the default
    /// AI prompts. One prompt per feature, all on
    /// <see cref="AiProvider.Echo"/> so dev + tests run offline. An
    /// admin can switch any prompt's <c>Provider</c> + edit the
    /// templates from the CP without a redeploy.</summary>
    private async Task EnsureDefaultAiPromptsAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        var seed = new (string Key, AiFeature Feature, string En, string Ar,
            string SystemPrompt, string UserTemplate)[]
        {
            ("question-filter", AiFeature.QuestionFilter,
                "Audience Question Safety Filter", "مصفّاة أمان أسئلة الجمهور",
                "You are a moderation assistant for a public maritime forum. Given an audience question, decide whether it is appropriate for a live Q&A: reject hate speech, personal attacks, off-topic content, advertising, or spam. Reply in JSON: {\"allowed\": bool, \"reason\": string}.",
                "Question: {text}"),
            ("faq-answer", AiFeature.Faq,
                "Event FAQ Assistant", "مساعد الأسئلة الشائعة للفعّالية",
                "You are the SIMF (Saudi International Maritime Forum) FAQ assistant. Answer concisely (1–3 sentences). Use Arabic if the question is in Arabic, English otherwise. If you do not know, say so and recommend asking the help desk.",
                "Question: {question}"),
            // Grounded on the live event context ({context}: programme sessions,
            // FAQ, booths — built server-side) so it answers from the real agenda,
            // not model priors. {locale} = the visitor's UI language.
            ("assistance", AiFeature.Assistance,
                "Visitor Concierge", "خدمة الزوّار",
                "You are a friendly concierge for SIMF (Saudi International Maritime Forum) visitors. Help with directions, the agenda, sessions, speakers, FAQ, and exhibition booths. Use ONLY the live event context provided with the question — never invent a session, time, hall, or booth. If the answer is not in that context, say you do not have that information and suggest asking the help desk. Be brief (1–3 sentences), polite, and culturally aware. Reply in Arabic when the visitor's language is 'ar', otherwise in English.",
                "Visitor language: {locale}\nVisitor question: {message}\n\nConversation so far (may be empty):\n{history}\n\nLive event context (programme sessions, FAQ, booths):\n{context}"),
            ("translate", AiFeature.Translate,
                "Text Translator", "مترجم النصوص",
                "Translate the text from {sourceLang} to {targetLang}. Reply with only the translation — no commentary, no quotes.",
                "{text}"),
            ("live-translation", AiFeature.LiveTranslation,
                "Live Speech Translator", "المترجم الحيّ للكلام",
                "Translate this in-progress transcript chunk from {sourceLang} to {targetLang}. Reply with only the translated chunk — keep punctuation light because chunks are concatenated client-side.",
                "{text}"),
            ("live-sign-language", AiFeature.LiveSignLanguage,
                "Live Sign-Language Gloss", "ترجمة الإشارة الحيّة",
                "Convert this in-progress transcript chunk into a glossed sign-language sequence suitable for a downstream avatar renderer. Keep glosses uppercase and space-separated.",
                "{text}"),
            // AI session-summary / محضر drafting.
            ("session-summary", AiFeature.SessionSummary,
                "Session Minutes (محضر) Drafter", "مُسوّد محضر الجلسة",
                "You are the rapporteur for the SIMF (Saudi International Maritime Forum). Draft concise, formal minutes (محضر) in Arabic covering the key points discussed, the recommendations, and who took part. Base the minutes primarily on the verbatim session transcript (subtitle) when one is provided; use the abstract only to fill gaps or when no transcript was captured. The Scientific Committee reviews and edits your draft before it is published.",
                "Session: {sessionTitle}\nSpeakers: {speakers}\nAbstract: {sessionAbstract}\nTranscript (subtitle): {transcript}\nTranscript (Arabic): {transcriptArabic}"),
            // Control Panel operator assistant — grounded on the CP page catalogue
            // ({pages}, one line per page the caller can access) so it can only ever
            // cite a real route the user is allowed to open.
            ("cp-assistant", AiFeature.CpAssistant,
                "Control Panel Assistant", "مساعد لوحة التحكم",
                "You are the assistant for the SIMF (Saudi International Maritime Forum) Control Panel — an administrator's help guide. The operator asks where to find a screen or how to configure something. You are given a directory of the Control Panel pages this operator can access, each with its exact route path. Answer briefly and practically, and ALWAYS cite the exact route path from the directory (for example /admin/sessions) so the operator can open it. Use ONLY routes that appear in the directory — never invent a path. If no listed page matches, say the operator may not have permission for it or it does not exist, and suggest asking an administrator. Reply in Arabic if the question is in Arabic, otherwise in English.",
                "Question: {question}\nOperator interface language: {locale}\nControl Panel pages available to this operator (name -> route):\n{pages}"),
        };

        var existing = await appDbContext.AiPrompts.AsNoTracking()
            .Select(p => p.Key).ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.SimfNow();
        var toSeed = 0;
        foreach (var (key, feature, en, ar, system, user) in seed)
        {
            if (existingSet.Contains(key)) continue;
            appDbContext.AiPrompts.Add(new SIMF.Domain.Ai.AiPrompt
            {
                Id = Guid.NewGuid(),
                Key = key,
                Feature = feature,
                DisplayName = en,
                DisplayNameArabic = ar,
                Provider = AiProvider.Echo,
                Model = Ai.EchoAiProvider.ModelName,
                SystemPrompt = system,
                UserPromptTemplate = user,
                Temperature = 0.2,
                MaxOutputTokens = 512,
                IsActive = true,
                Version = 1,
                CreatedAt = now,
                UpdatedByUserId = actorUserId,
            });
            toSeed++;
        }
        if (toSeed > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Default AI prompts ensured (seeded {NewCount} of {Total}).",
            toSeed, seed.Length);
    }

    /// <summary>Give the seeded, Approved demo accounts a
    /// couple of OVERLAPPING interests so the "قابل أشخاص مثلك" recommender returns
    /// matches on a fresh database (it needs the caller AND at least one candidate to
    /// each carry an overlapping interest). App-DB-only — it links existing
    /// <see cref="UserInterest"/> rows to the demo profiles' <c>UserProfile.Interests</c>
    /// M-to-M; no Identity change, no new account, no migration. Idempotent: a profile
    /// that already has ANY interest is left untouched, so an admin edit is never
    /// overwritten.
    /// <para>This used to run over a hand-copied list of four emails
    /// while <see cref="DemoAccounts"/> holds eight profile-carrying accounts, so
    /// moderator@ / exhibitor@ / media@ / sponsor@ never got an interest — and the
    /// server completeness rule (which demands ≥ 1 interest) kept them
    /// <c>profileComplete=false</c> forever, no matter what the tester uploaded. It
    /// now walks <see cref="DemoProfileEmails"/>, so the two lists cannot drift
    /// again.</para></summary>
    private async Task EnsureDemoVisitorInterestsAsync(CancellationToken cancellationToken)
    {
        // Shared interests (from the baseline interest lookup) so every pair of these
        // visitors overlaps.
        var sharedInterestNames = new[]
        {
            "Maritime Security",
            "Naval Defence Technologies",
            "Maritime Cybersecurity",
        };
        var sharedInterests = await appDbContext.Interests
            .Where(interest => interest.IsActive && sharedInterestNames.Contains(interest.Name))
            .ToListAsync(cancellationToken);
        if (sharedInterests.Count == 0)
        {
            return; // the interest lookup is empty — nothing to link.
        }

        var linked = 0;
        foreach (var email in DemoProfileEmails)
        {
            var user = await accounts.FindByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                continue;
            }
            var profile = await appDbContext.UserProfiles
                .Include(p => p.Interests)
                .SingleOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (profile is null || profile.Interests.Count > 0)
            {
                continue; // no profile, or already has interests — idempotent skip.
            }
            foreach (var interest in sharedInterests)
            {
                profile.Interests.Add(interest);
            }
            linked++;
        }

        if (linked > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Demo visitor interests ensured (linked {Count} profile(s) for Meet-People).",
            linked);
    }

    /// <summary>Give every seeded demo profile the two images the server
    /// completeness rule demands: the identity document (all registrants) and the
    /// face photo / avatar (required for a male registrant, and every demo profile
    /// is seeded <see cref="Gender.Male"/>). Without them a demo account boots with
    /// <c>profileComplete=false</c> and the app parks it on the "complete your
    /// profile" wall, so none of the eight accounts was usable out of the box.
    ///
    /// <para>The bytes go through the ordinary <see cref="IFileService"/> pipeline —
    /// the ID document and the avatar are encrypted-at-rest services, so they can
    /// NOT be pre-placed on disk like the public speaker photos. The
    /// pointers written back (<c>UserProfile.IdImageFileId</c> /
    /// <c>SimfUser.AvatarFileId</c>) are the bare StoredFile ids, exactly as
    /// the upload endpoints write them.</para>
    ///
    /// <para>Idempotent and self-healing: an account is re-seeded only when its
    /// pointer is empty <b>or</b> no longer resolves to content, so a re-run never
    /// uploads twice yet a <i>dangling</i> pointer is repaired. Testing the pointer
    /// for emptiness alone is not enough — a non-empty pointer proves only that
    /// something was uploaded once, so a database restored past its file store (or
    /// a moved storage root) left every demo account permanently broken: the pointer
    /// looked healthy, the seeder skipped it, and the image 404ed forever.</para>
    ///
    /// <para>Cross-DB safe — the App-side profile and the Identity-side user are
    /// saved through their own contexts, never in one transaction.</para></summary>
    private async Task EnsureDemoAccountAssetsAsync(CancellationToken cancellationToken)
    {
        var seeded = 0;
        foreach (var email in DemoProfileEmails)
        {
            var user = await accounts.FindByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                continue; // demo accounts not seeded in this environment.
            }
            var profile = await appDbContext.UserProfiles
                .SingleOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (profile is null)
            {
                continue;
            }

            if (await NeedsReseedAsync(profile.IdImageFileId, cancellationToken))
            {
                var idDocument = await fileService.UploadAsync(
                    new UploadFileCommand(
                        FileService.IdDocument, user.Id, DemoIdDocumentPng,
                        "demo-id-document.png", "image/png", user.Id, FailClosed: false),
                    cancellationToken);
                profile.IdImageFileId = idDocument.Id;
                profile.UpdatedAt = timeProvider.SimfNow();
                await appDbContext.SaveChangesAsync(cancellationToken);
                seeded++;
            }

            if (await NeedsReseedAsync(user.AvatarFileId, cancellationToken))
            {
                var avatar = await fileService.UploadAsync(
                    new UploadFileCommand(
                        FileService.Avatar, user.Id, DemoAvatarPng,
                        "demo-avatar.png", "image/png", user.Id, FailClosed: false),
                    cancellationToken);
                user.AvatarFileId = avatar.Id;
                await accounts.UpdateAsync(user).EnsureSuccessAsync();
                seeded++;
            }
        }

        logger.LogInformation(
            "Demo account assets ensured (seeded {Count} missing image(s)).", seeded);
    }

    /// <summary>True when an image pointer needs re-seeding: it is empty, it is not
    /// a stored-file id at all (a relative path left on a row that predates the
    /// unified file store), or it is a well-formed id whose content has gone. The
    /// last case is the one a plain emptiness test misses.</summary>
    private async Task<bool> NeedsReseedAsync(string? pointer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pointer)) { return true; }
        if (!Guid.TryParse(pointer, out var fileId)) { return true; }
        return !await fileService.ContentExistsAsync(fileId, cancellationToken);
    }

    /// <summary>The same test for a pointer that is already a real
    /// <see cref="Guid"/>. There is no "not an id at all" case to consider here,
    /// which is the whole point of having retyped the column.</summary>
    private async Task<bool> NeedsReseedAsync(Guid? fileId, CancellationToken cancellationToken)
    {
        if (fileId is not { } id) { return true; }
        return !await fileService.ContentExistsAsync(id, cancellationToken);
    }
}
