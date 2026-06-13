// Tests: SIMF.Api.Tests/IdentitySeederTests.cs (super-admin, TOTP, audit,
//        idempotency, D-377 baseline lookups + core content,
//        D-390 2FA-disable-persists-across-reseed)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

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
    SimfAppDbContext appDbContext,
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

        // Issue-1 — seed the full page-and-action permission catalogue
        // (SIMF-RPM-001 §8, PermissionCatalog). Idempotent by Code, so it is
        // safe on every boot. Baseline non-Administrator roles get their
        // seeded grants from PermissionDef.BaselineRoles (GateOperator → the
        // gate operator pair; PublicRelations → the invitation + VIP set).
        // Administrator is never granted per-code: it carries the wildcard
        // permission ("*") minted into its token and so holds every
        // permission implicitly. The six pre-catalogue codes (D-148 gate
        // triad, D-168 PR/VIP triad) keep their exact strings and grants.
        foreach (var permission in PermissionCatalog.All)
        {
            await EnsurePermissionAsync(
                permission.Code, permission.Page, permission.Action,
                permission.DisplayName, permission.BaselineRoles, cancellationToken);
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

        // D-101 (amended D-390): keep the configured TOTP secret in sync on an
        // EXISTING admin row, but NEVER force two-factor back on. The original
        // D-101 re-enabled 2FA on every boot so the super-admin always carried
        // the second factor — but that meant an operator who deliberately
        // disabled the super-admin's 2FA found it switched back on after the
        // next restart. D-390 reverses that: the disabled choice must survive a
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

        // D-124: the original seed names redundantly prefixed the UserType
        // ("Visitor — General", "Other — Staff") even though UserType is a
        // separate column on the same row. Rename the rows in place on any
        // DB that still carries the old names — the CP grid now surfaces
        // UserType as its own column (D-125) so the prefix is noise. The
        // rename runs before the EnsureProfileTypeAsync calls below so the
        // ensure step is a true no-op afterwards.
        await RenameProfileTypeIfPresentAsync(
            "Visitor — General", "General", "زائر — عام", "عام",
            UserType.Visitor, cancellationToken);
        // D-186: legacy "Other — Staff" rows are under UserType.Visitor
        // after the data migration; rename + audience-vs-partner state
        // is preserved on the row's IsVisitor flag (false for Staff).
        await RenameProfileTypeIfPresentAsync(
            "Other — Staff", "Staff", "أخرى — فريق", "فريق",
            UserType.Visitor, cancellationToken);

        // C5 (D-371) — the owner fixed the visitor self-registration type's
        // name as "Normal" (عادي); rename any DB still carrying the P7-era
        // "General" row in place (same idempotent machinery as D-124 above).
        await RenameProfileTypeIfPresentAsync(
            "General", "Normal", "عام", "عادي",
            UserType.Visitor, cancellationToken);

        // P7 — seed the initial ProfileTypes set so the create / pending
        // pages have non-empty pickers from first boot. D-186 collapsed
        // every seeded row under UserType.Visitor; the partner-side
        // ones (Staff / Media / Sponsor) carry IsVisitor=false so the
        // CP "Others" approval queue finds them. C5 (D-371): "Normal" is
        // the single audience-side type a visitor self-registers under.
        await EnsureProfileTypeAsync(
            "Normal", "عادي", "#3B82F6",
            isVisitor: true, MobileAppRole.None, cancellationToken);
        // D-161 — Staff is the canonical operational partner-side profile
        // type; the default mobile-app role is Staff (can perform gate
        // operations, look up attendees, print badges). Admins seed the
        // remaining operational types (Volunteer → Staff,
        // Programme Coordinator / Operations Lead → Moderator,
        // Exhibitor / Sponsor / Speaker → None) via the CP runtime.
        await EnsureProfileTypeAsync(
            "Staff", "فريق", "#10B981",
            isVisitor: false, MobileAppRole.Staff, cancellationToken);
        // D-163 (PDF §2.5) — partner-tier seed expanded to ship Media
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

        // D-174 (gap doc G11, Mockup page 39) — seed the cybersecurity
        // policy content blocks the Flutter "سياسات وضوابط الأمن
        // السيبراني" screen reads. Idempotent: only writes the row when
        // missing, matches the existing EnsureProfileTypeAsync pattern.
        await EnsureCybersecurityPolicyContentAsync(admin.Id, cancellationToken);

        // Seed the public marketing landing's hero CMS text blocks so the
        // Website's /content/site proxy can serve them and the CP CMS editor
        // can manage them. Idempotent — same insert-when-absent shape.
        await EnsureLandingHeroContentAsync(admin.Id, cancellationToken);

        // Seed the landing's editorial sections below the hero — About, the
        // global-landscape stats strip, the Pillars header and the Goals
        // block — so the same /content/site proxy + CP CMS editor drive them
        // instead of the page's hardcoded copy. Idempotent, additive data.
        await EnsureLandingSectionsContentAsync(admin.Id, cancellationToken);

        // D-377 — baseline lookups + core app content. Interests and the
        // organisation lookup are REQUIRED by the visitor profile save
        // (1–10 interests + an organisation pick), so an environment where
        // either table is empty silently makes registration impossible —
        // exactly what happened on the first production install (the rows
        // were entered by hand through the admin API). Seed only when the
        // table is completely empty: admins own the lists at runtime and a
        // deliberate deletion must never be re-added on the next boot.
        await EnsureBaselineInterestsAsync(admin.Id, cancellationToken);
        await EnsureBaselineOrganisationsAsync(admin.Id, cancellationToken);

        // D-377 — the app's terms + about content blocks (Page 009 / Page 037
        // render their empty states without them). Insert-when-absent, same
        // shape as the cyber/landing content seeds above.
        await EnsureCoreAppContentAsync(admin.Id, cancellationToken);

        // D-345 — seed a demo speaker roster so the public /app/speakers list
        // (and the Website speakers strip + the app speakers screen) render a
        // populated, realistic set out of the box instead of a single lonely
        // row. Idempotent by Code; admins manage / replace with real speakers
        // via the CP, and can deactivate these at will.
        await EnsureDemoSpeakersAsync(admin.Id, cancellationToken);

        // D-347 — seed a few past-edition rows (and enrich the existing one) so
        // the public Archive timeline shows several years with real detail
        // (title / summary / place / date / counters), which the Website's
        // per-year archive page renders. Idempotent by Year.
        await EnsureDemoArchiveEditionsAsync(admin.Id, cancellationToken);

        // D-348 — seed sponsors + media partners with test logos so the public
        // partners strip shows a populated logo row (was name-only text).
        await EnsureDemoPartnersAsync(admin.Id, cancellationToken);

        // D-176 (gap doc G12) — seed the default AI prompt catalogue.
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

    /// <summary>D-148 — idempotent insert of a Permission row + grants to
    /// the named baseline roles. Safe to re-run on every startup.</summary>
    private async Task EnsurePermissionAsync(
        string code, string page, string action, string displayName,
        IReadOnlyList<string> grantToRoles,
        CancellationToken cancellationToken)
    {
        var permission = await dbContext.Permissions
            .SingleOrDefaultAsync(p => p.Code == code, cancellationToken);
        if (permission is null)
        {
            permission = new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Page = page,
                Action = action,
                DisplayName = displayName,
            };
            dbContext.Permissions.Add(permission);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var roleName in grantToRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) { continue; }
            var grantExists = await dbContext.RolePermissions
                .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id,
                    cancellationToken);
            if (!grantExists)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>D-124 — idempotent rename. When a row with the old Name
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
        legacy.UpdatedAt = timeProvider.GetUtcNow();
        // D-167: ProfileType lives on App DB.
        await appDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "D-124: renamed seeded ProfileType '{OldName}' to '{NewName}' for {UserType}.",
            oldName, newName, userType);
    }

    /// <summary>P7 — idempotent ProfileTypes seed (lookup by Name + UserType).
    /// D-161 added the <paramref name="mobileAppRole"/> parameter so seed
    /// rows can ship with the right mobile-app authority out of the box.
    /// D-186: every seeded profile type now goes under <c>UserType.Visitor</c>;
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
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        // D-167: ProfileType lives on App DB.
        await appDbContext.SaveChangesAsync(cancellationToken);
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

    /// <summary>D-174 (gap doc G11, Mockup page 39) — seed the
    /// cybersecurity-policy content blocks the Flutter mobile app reads
    /// at <c>/api/v1/content/cyber.*</c>. Idempotent: each block is
    /// inserted only when its key is absent (the same shape
    /// EnsureProfileTypeAsync uses). The text is the page-39 mockup
    /// verbatim (Arabic) + a paired English translation so the existing
    /// bilingual ContentBlock contract is respected.</summary>
    private async Task EnsureCybersecurityPolicyContentAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        // (Key, EN, AR) — matches the page-39 layout:
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

        var now = timeProvider.GetUtcNow();
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
            "D-174: cybersecurity policy content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

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
            (LandingHeroContentKeys.MetaDate,
             "23 — 25 November 2026",
             "23 — 25 نوفمبر 2026"),
            (LandingHeroContentKeys.MetaVenue,
             "Sofitel Riyadh Hotel & Convention Centre",
             "فندق ومركز مؤتمرات سوفيتيل الرياض"),
            (LandingHeroContentKeys.CtaSecondary,
             "Browse the programme",
             "تصفّح البرنامج"),
        };

        var now = timeProvider.GetUtcNow();
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
            // About — Mockup §01.
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

            // Global-landscape stats strip — Mockup §02.
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

            // Pillars header — Mockup §03.
            (LandingSectionContentKeys.PillarsEyebrow, "Key Pillars", "المحاور الرئيسية"),
            (LandingSectionContentKeys.PillarsHeading, "Key Pillars", "المحاور الرئيسية"),
            (LandingSectionContentKeys.PillarsBody,
             "Building a comprehensive strategic vision that addresses energy systems, trade, and the link between surface and depths through five core pillars that anchor maritime security and global economic stability.",
             "لصياغة رؤية استراتيجية شاملة تعالج منظومات الطاقة والتجارة والاتصال بين السطح والأعماق عبر خمسة محاور رئيسية تشكل ركائز الأمن البحري واستقرار الاقتصاد العالمي."),

            // Goals — Mockup §08.
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

        var now = timeProvider.GetUtcNow();
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

    /// <summary>D-377 — baseline interests for the visitor profile picker.
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

        var now = timeProvider.GetUtcNow();
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
            "D-377: baseline interests seeded ({Count} rows; table was empty).",
            seed.Length);
    }

    /// <summary>D-377 — baseline organisation lookup for the profile's
    /// required الجهة pick (B3 — D-221). Includes an explicit
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

        var now = timeProvider.GetUtcNow();
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
            "D-377: baseline organisations seeded ({Count} rows; table was empty).",
            seed.Length);
    }

    /// <summary>D-377 — the app's terms + about content blocks (the same
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

        var now = timeProvider.GetUtcNow();
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
            "D-377: core app content blocks ensured (seeded {NewCount} of {Total}).",
            seed.Length - existingKeys.Count, seed.Length);
    }

    /// <summary>D-345 — idempotent demo speaker roster. Inserts any missing
    /// <c>DEMO-SPK-*</c> speaker so the anonymous <c>GET /app/speakers</c> read
    /// returns a populated, on-theme set (maritime-security / supply-chain) for
    /// the Website strip and the app screen. Country is left null on purpose:
    /// <c>Speaker.CountryId</c> is a real FK to <c>Country</c>, and seeding a
    /// country row is out of scope here. Active + ordered so they surface.</summary>
    private async Task EnsureDemoSpeakersAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        // PhotoRelativePath carries a test portrait URL (D-346 — "use any photo
        // for testing"): the field already exists (no migration), the
        // /content/site proxy now passes it through, and the Website speaker
        // card renders it when present. Placeholder portraits from pravatar.cc.
        var seed = new (string Code, string Name, string NameArabic, string Rank, int Order, string Photo)[]
        {
            ("DEMO-SPK-01", "Dr. Sarah Al-Otaibi", "د. سارة العتيبي", "Maritime Security Strategist", 10, "https://i.pravatar.cc/300?img=47"),
            ("DEMO-SPK-02", "Adm. James Whitmore", "الأدميرال جيمس ويتمور", "Former Fleet Commander", 20, "https://i.pravatar.cc/300?img=12"),
            ("DEMO-SPK-03", "Prof. Khalid Al-Harbi", "أ.د. خالد الحربي", "Blue-Economy Researcher", 30, "https://i.pravatar.cc/300?img=33"),
            ("DEMO-SPK-04", "Dr. Liang Chen", "د. ليانغ تشين", "Global Supply-Chain Economist", 40, "https://i.pravatar.cc/300?img=68"),
            ("DEMO-SPK-05", "Capt. Maria Santos", "النقيب ماريا سانتوس", "Port Operations Expert", 50, "https://i.pravatar.cc/300?img=45"),
            ("DEMO-SPK-06", "Dr. Yuki Tanaka", "د. يوكي تاناكا", "Naval Technology Advisor", 60, "https://i.pravatar.cc/300?img=60"),
            ("DEMO-SPK-07", "Cdre. Olivier Dubois", "العميد أوليفييه دوبوا", "Coastal Defence Specialist", 70, "https://i.pravatar.cc/300?img=15"),
        };

        var now = timeProvider.GetUtcNow();
        var codes = seed.Select(x => x.Code).ToList();
        var existing = await appDbContext.Speakers
            .Where(s => codes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, cancellationToken);

        var inserted = 0;
        foreach (var (code, name, nameArabic, rank, order, photo) in seed)
        {
            if (existing.TryGetValue(code, out var current))
            {
                // Already seeded — backfill the test photo if it is missing so an
                // earlier photo-less seed still ends up with a portrait.
                if (string.IsNullOrEmpty(current.PhotoRelativePath))
                {
                    current.PhotoRelativePath = photo;
                }
                continue;
            }
            appDbContext.Speakers.Add(new SIMF.Domain.Programme.Speaker
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                NameArabic = nameArabic,
                Rank = rank,
                DisplayOrder = order,
                PhotoRelativePath = photo,
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
            inserted++;
        }

        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Demo speakers ensured (inserted {NewCount}, total {Total}).",
            inserted, seed.Length);
    }

    /// <summary>D-347 — idempotent past-edition roster (by Year). Inserts any
    /// missing year and backfills the detail fields (title / summary / place /
    /// date / session count) on a row that was created counters-only, so the
    /// public Archive timeline and the per-year detail page have real content.
    /// No migration — every column already exists on <c>ArchiveEdition</c>.</summary>
    private async Task EnsureDemoArchiveEditionsAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        var seed = new (int Year, string TitleEn, string TitleAr, string SummaryEn, string SummaryAr,
            string LocationEn, string LocationAr, string DateLabelEn, string DateLabelAr,
            int Attendees, int Sessions, int Speakers)[]
        {
            (2022, "SIMF 2022", "سيمف 2022",
             "The inaugural edition — charting a course for regional maritime security.",
             "النسخة الأولى — رسم مسار الأمن البحري الإقليمي.",
             "Riyadh · Saudi Arabia", "الرياض · المملكة العربية السعودية",
             "November 2022 · 3 days", "نوفمبر 2022 · 3 أيام", 800, 24, 30),
            (2023, "SIMF 2023", "سيمف 2023",
             "Securing tomorrow's seas — resilience across the maritime domain.",
             "تأمين بحار الغد — المرونة عبر القطاع البحري.",
             "Riyadh · Saudi Arabia", "الرياض · المملكة العربية السعودية",
             "November 2023 · 3 days", "نوفمبر 2023 · 3 أيام", 1000, 32, 35),
            (2024, "SIMF 2024", "سيمف 2024",
             "Resilient maritime supply chains for a connected world.",
             "سلاسل إمداد بحرية مرنة لعالم مترابط.",
             "Riyadh · Saudi Arabia", "الرياض · المملكة العربية السعودية",
             "November 2024 · 3 days", "نوفمبر 2024 · 3 أيام", 1100, 38, 38),
            (2025, "SIMF 2025", "سيمف 2025",
             "The fourth edition — the future of seabed security and supply chains.",
             "النسخة الرابعة — مستقبل أمن قاع البحار وسلاسل الإمداد.",
             "Riyadh · Saudi Arabia", "الرياض · المملكة العربية السعودية",
             "November 2025 · 3 days", "نوفمبر 2025 · 3 أيام", 1200, 40, 40),
        };

        var now = timeProvider.GetUtcNow();
        var years = seed.Select(x => x.Year).ToList();
        var existing = await appDbContext.ArchiveEditions
            .Where(e => years.Contains(e.Year))
            .ToDictionaryAsync(e => e.Year, cancellationToken);

        var inserted = 0;
        foreach (var s in seed)
        {
            if (existing.TryGetValue(s.Year, out var current))
            {
                // Backfill detail on a counters-only row created earlier.
                if (string.IsNullOrWhiteSpace(current.TitleEn)) { current.TitleEn = s.TitleEn; }
                if (string.IsNullOrWhiteSpace(current.TitleAr)) { current.TitleAr = s.TitleAr; }
                current.SummaryEn ??= s.SummaryEn;
                current.SummaryAr ??= s.SummaryAr;
                current.LocationEn ??= s.LocationEn;
                current.LocationAr ??= s.LocationAr;
                current.DateLabelEn ??= s.DateLabelEn;
                current.DateLabelAr ??= s.DateLabelAr;
                if (current.Sessions == 0) { current.Sessions = s.Sessions; }
                continue;
            }
            appDbContext.ArchiveEditions.Add(new SIMF.Domain.Archive.ArchiveEdition
            {
                Id = Guid.NewGuid(),
                Year = s.Year,
                TitleEn = s.TitleEn,
                TitleAr = s.TitleAr,
                SummaryEn = s.SummaryEn,
                SummaryAr = s.SummaryAr,
                LocationEn = s.LocationEn,
                LocationAr = s.LocationAr,
                DateLabelEn = s.DateLabelEn,
                DateLabelAr = s.DateLabelAr,
                Attendees = s.Attendees,
                Sessions = s.Sessions,
                Speakers = s.Speakers,
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
            inserted++;
        }

        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Demo archive editions ensured (inserted {NewCount}, total {Total}).",
            inserted, seed.Length);
    }

    /// <summary>D-348 — idempotent sponsors + media partners with test logos so
    /// the public partners strip renders a populated logo row instead of
    /// name-only text. Idempotent by Name; backfills a test logo onto any active
    /// row that still has none (incl. the pre-existing demo rows). Logos are test
    /// placeholders (the column normally holds an asset path; the owner asked for
    /// any test image while real artwork is pending).</summary>
    private async Task EnsureDemoPartnersAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        const string logoBase = "https://placehold.co/260x130/ffffff/0a2e6b?text=";
        string Logo(string label) => logoBase + Uri.EscapeDataString(label);
        var now = timeProvider.GetUtcNow();

        var sponsors = new (string Name, string NameAr, SponsorTier Tier, int Order)[]
        {
            ("Maritime Defense Systems", "نظم الدفاع البحري", SponsorTier.Platinum, 10),
            ("Gulf Port Authority", "هيئة موانئ الخليج", SponsorTier.Gold, 20),
            ("Blue Horizon Logistics", "آفاق زرقاء للخدمات اللوجستية", SponsorTier.Gold, 30),
            ("Coastal Shield Technologies", "تقنيات الدرع الساحلي", SponsorTier.Silver, 40),
        };
        var sponsorNames = sponsors.Select(x => x.Name).ToList();
        var existingSponsors = await appDbContext.Sponsors
            .Where(s => sponsorNames.Contains(s.Name)).Select(s => s.Name)
            .ToListAsync(cancellationToken);
        foreach (var sp in sponsors)
        {
            if (existingSponsors.Contains(sp.Name)) { continue; }
            appDbContext.Sponsors.Add(new SIMF.Domain.Sponsors.Sponsor
            {
                Id = Guid.NewGuid(),
                Name = sp.Name,
                NameArabic = sp.NameAr,
                Tier = sp.Tier,
                DisplayOrder = sp.Order,
                LogoRelativePath = Logo(sp.Name),
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
        }

        var partners = new (string Name, string NameAr, int Order)[]
        {
            ("Maritime News Network", "شبكة الأخبار البحرية", 10),
            ("Naval Affairs Review", "مجلة الشؤون البحرية", 20),
            ("Sea Trade Daily", "تجارة البحار اليومية", 30),
        };
        var partnerNames = partners.Select(x => x.Name).ToList();
        var existingPartners = await appDbContext.MediaPartners
            .Where(m => partnerNames.Contains(m.Name)).Select(m => m.Name)
            .ToListAsync(cancellationToken);
        foreach (var mp in partners)
        {
            if (existingPartners.Contains(mp.Name)) { continue; }
            appDbContext.MediaPartners.Add(new SIMF.Domain.PublicRelations.MediaPartner
            {
                Id = Guid.NewGuid(),
                Name = mp.Name,
                NameArabic = mp.NameAr,
                DisplayOrder = mp.Order,
                LogoRelativePath = Logo(mp.Name),
                IsActive = true,
                CreatedBy = actorUserId,
                CreatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        // Backfill a test logo onto any active row that still has none (e.g. the
        // pre-existing single sponsor + partner) so the strip is all logos.
        var logolessSponsors = await appDbContext.Sponsors
            .Where(s => s.IsActive && (s.LogoRelativePath == null || s.LogoRelativePath == ""))
            .ToListAsync(cancellationToken);
        foreach (var s in logolessSponsors)
        {
            s.LogoRelativePath = Logo(string.IsNullOrWhiteSpace(s.Name) ? "Sponsor" : s.Name);
        }
        var logolessPartners = await appDbContext.MediaPartners
            .Where(m => m.IsActive && (m.LogoRelativePath == null || m.LogoRelativePath == ""))
            .ToListAsync(cancellationToken);
        foreach (var m in logolessPartners)
        {
            m.LogoRelativePath = Logo(string.IsNullOrWhiteSpace(m.Name) ? "Partner" : m.Name);
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo partners ensured (sponsors + media partners with logos).");
    }

    /// <summary>D-176 (gap doc G12) — idempotently seeds the default
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
            ("assistance", AiFeature.Assistance,
                "Visitor Concierge", "خدمة الزوّار",
                "You are a friendly concierge for SIMF visitors. Help with directions, agenda, speakers, and general guidance. Be brief, polite, and culturally aware. Reply in the same language as the visitor.",
                "{message}"),
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
            // P4.1 — D-238: AI session-summary / محضر drafting (Mockup screen 34).
            ("session-summary", AiFeature.SessionSummary,
                "Session Minutes (محضر) Drafter", "مُسوّد محضر الجلسة",
                "You are the rapporteur for the SIMF (Saudi International Maritime Forum). From the session metadata you are given, draft concise, formal minutes (محضر) in Arabic covering the key points discussed, the recommendations, and who took part. The Scientific Committee reviews and edits your draft before it is published.",
                "Session: {sessionTitle}\nSpeakers: {speakers}\nAbstract: {sessionAbstract}"),
        };

        var existing = await appDbContext.AiPrompts.AsNoTracking()
            .Select(p => p.Key).ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow();
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
                Model = "echo",
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
            "D-176: default AI prompts ensured (seeded {NewCount} of {Total}).",
            toSeed, seed.Length);
    }
}
