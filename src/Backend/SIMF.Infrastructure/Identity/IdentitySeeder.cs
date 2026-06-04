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

        // D-101: idempotently enforce the configured TOTP secret on an
        // EXISTING admin row. Pre-D-101 the TOTP-setup block was inside
        // CreateSuperAdminAsync — it only ran on first creation, so an
        // operator who set SuperAdmin:TotpSecret AFTER the admin row was
        // created (or who rotated the secret in appsettings) ended up
        // with a row whose active authenticator key did not match config
        // and whose TwoFactorEnabled flag was still false. The result was
        // the owner's "TOTP not working" complaint: sign-in bypassed the
        // second factor entirely. Compare the active key to config and
        // re-apply when they differ — the operator's appsettings value
        // is authoritative.
        if (!string.IsNullOrWhiteSpace(settings.TotpSecret))
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
            if (!admin.TwoFactorEnabled)
            {
                await accounts.SetTwoFactorEnabledAsync(
                    admin, true, cancellationToken).EnsureSuccessAsync();
                logger.LogInformation(
                    "Super-admin TwoFactorEnabled re-enabled for {Email}.",
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

        // P7 — seed the initial ProfileTypes set so the create / pending
        // pages have non-empty pickers from first boot. D-186 collapsed
        // every seeded row under UserType.Visitor; the partner-side
        // ones (Staff / Media / Sponsor) carry IsVisitor=false so the
        // CP "Others" approval queue finds them.
        await EnsureProfileTypeAsync(
            "General", "عام", "#3B82F6",
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
