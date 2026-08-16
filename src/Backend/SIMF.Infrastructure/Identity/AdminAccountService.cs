// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs,
//        SIMF.Api.Tests/AdminCreateUserTests.cs,
//        SIMF.Api.Tests/ControlPanelTwoFactorEnrolmentTests.cs (a created
//        admin is TwoFactorEnabled AND can still complete a first sign-in),
//        SIMF.Api.Tests/WalkInRegistrationTests.cs +
//        SIMF.Api.Tests/AdminAccountMobileTests.cs (the desk-created profile's
//        mobile lands in the one canonical column and the two lockstep ones)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Domain.Notifications;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin-driven user-management use cases: reset 2FA, create a new CP user,
/// list every account.
///
/// <para>Implements five focused interfaces that were split out of a
/// monolithic <c>IAdminAccountService</c>. The implementation stays in one
/// class for now — the interface split is the architectural improvement the
/// callers care about, and splitting the implementation into five 150- to
/// 250-line classes is a follow-up.</para>
/// </summary>
internal sealed partial class AdminAccountService(
    IUserAccountRepository accounts,
    RoleManager<SimfRole> roleManager,
    IRefreshTokenRepository refreshTokenRepository,
    IRecoveryCodeService recoveryCodes,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    // Renders the BulkBadgeDelivery cover note (DB override or the
    // code-owned default) for the emailed bulk-badge ZIP. Never throws.
    IEmailTemplateResolver emailTemplates,
    IAuditLog auditLog,
    IUserExcelService excel,
    // qrIdMinter is used by the approve flow (AdminAccountService.Approval.cs)
    // to mint the QR on approval; the create path no longer mints one.
    IQrIdMinter qrIdMinter,
    ITransactionRunner transactionRunner,
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext,
    IUserProfileRepository profiles,
    IPiiEncryptor pii,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    // The standby walk-in capability (auto-approve + quick register).
    // IOptionsMonitor so arming it in appsettings / set-env-* takes effect
    // without a restart.
    IOptionsMonitor<WalkInModeOptions> walkInMode,
    // Read to decide whether forcing TwoFactorEnabled at creation is safe;
    // see CreateAccountAsync.
    IOptions<IdentityLifecycleOptions> lifecycleOptions,
    ILogger<AdminAccountService> logger)
    : IAdminTwoFactorService,
      IAdminUserApprovalService,
      IAdminUserProvisioningService,
      IAdminUserBulkService
{
    /// <summary>
    /// The desk's ORIGINAL presence rules, moved out of
    /// <c>AdminWalkInRegistrationRequestValidator</c> so they can be skipped when
    /// the quick-register mode is armed. Messages are the validator's word for
    /// word, so with the mode disarmed a caller sees exactly what it saw before.
    /// Shape rules (lengths, Luhn, E.164, plate) stay in the validator and always
    /// apply.
    /// </summary>
    private static void EnsureFullDeskFields(AdminWalkInRegistrationRequest request)
    {
        // This one was DROPPED when the presence rules moved out of
        // the validator, so a blank or one-character display name started
        // returning 200 with the mode disarmed. The badge prints this name.
        RequireDeskField(
            request.DisplayName is { Length: >= 2 } displayName
                && !string.IsNullOrWhiteSpace(displayName),
            "Display name is required.", "الاسم المعروض مطلوب.");
        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.ArabicName),
            "Arabic name is required.", "الاسم بالعربية مطلوب.");
        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.EnglishName),
            "English name is required.", "الاسم بالإنجليزية مطلوب.");
        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.NationalityCode),
            "Nationality is required.", "الجنسية مطلوبة.");
        RequireDeskField(
            request.OrganisationId is { } organisationId && organisationId != Guid.Empty,
            "Organisation is required.", "الجهة مطلوبة.");

        if (request.IsSaudi)
        {
            RequireDeskField(
                !string.IsNullOrWhiteSpace(request.NationalId),
                "Saudi national ID is required for Saudi nationals.",
                "الهوية الوطنية مطلوبة للمواطنين السعوديين.");
        }
        else
        {
            RequireDeskField(
                !string.IsNullOrWhiteSpace(request.IqamaNumber)
                    || !string.IsNullOrWhiteSpace(request.PassportNumber),
                "An Iqama or passport number is required.",
                "رقم الإقامة أو جواز السفر مطلوب.");
        }

        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.SaudiMobile)
                || !string.IsNullOrWhiteSpace(request.InternationalMobile),
            "A mobile number is required (Saudi or international).",
            "رقم الجوال مطلوب (سعودي أو دولي).");
    }

    /// <summary>
    /// The quick-register floor. Everything else the full desk demands is
    /// optional, but two things are not:
    ///
    /// <para>A NAME, because a badge with no name on it is unusable at a gate.
    /// Any single script satisfies it; the service mirrors it into the other
    /// language column, which is what keeps the NOT NULL pair valid.</para>
    ///
    /// <para>An IDENTITY DOCUMENT, because it is the only thing preventing one
    /// person from collecting several badges: the duplicate-identity guard and
    /// its three filtered unique indexes key off a blind index of it. The
    /// plaintext columns are AES-GCM encrypted with a random nonce, so an id not
    /// captured at the desk can never be reconstructed afterwards. Made optional
    /// only by an explicit configuration choice, which the operator has to take
    /// knowingly.</para>
    /// </summary>
    private static void EnsureQuickDeskFloor(
        AdminWalkInRegistrationRequest request, bool requireIdentityDocument = true)
    {
        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.ArabicName)
                || !string.IsNullOrWhiteSpace(request.EnglishName)
                || !string.IsNullOrWhiteSpace(request.DisplayName),
            "A name is required.", "الاسم مطلوب.");

        if (!requireIdentityDocument) { return; }

        RequireDeskField(
            !string.IsNullOrWhiteSpace(request.NationalId)
                || !string.IsNullOrWhiteSpace(request.IqamaNumber)
                || !string.IsNullOrWhiteSpace(request.PassportNumber),
            "An identity document number is required (national ID, Iqama or passport).",
            "رقم وثيقة الهوية مطلوب (الهوية الوطنية أو الإقامة أو جواز السفر).");
    }

    private static void RequireDeskField(bool satisfied, string english, string arabic)
    {
        if (satisfied) { return; }
        throw new ApiException(ErrorCodes.ValidationFailed, 400, english, arabic);
    }

    private const string AdministratorRole = "Administrator";
    private const string AuthenticatorProvider = "[AspNetUserStore]";
    private const string ActiveSecretTokenName = "AuthenticatorKey";
    private const string PendingSecretProvider = "[SIMF]";
    private const string PendingSecretTokenName = "PendingAuthenticatorKey";

    public async Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await accounts.FindByIdAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على حساب المسؤول.");

        var target = await accounts.FindByEmailAsync(request.Email);
        if (target is null)
        {
            await AuditFailure(actorUserId, request.Email, null,
                ErrorCodes.AuthAccountNotFound, cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "No account was found for this email address.",
                "لم يتم العثور على حساب بهذا البريد الإلكتروني.");
        }

        // Deliberately NOT tier-scoped, and that is a decision rather than
        // an oversight. The route sits under /admin/admins/ and is gated on
        // Admins.ResetTwoFactor, so it reads like an admins-only action. The
        // requirement is "an Administrator resets another USER's 2FA", and
        // AdminResetTwoFactorTests enrols a VISITOR in TOTP and resets it here. A
        // tier guard was written, broke both of those tests, and was reverted: the
        // help-desk reset has to reach whoever actually lost their authenticator.
        // The route name and the Admins.* code are the misleading part, not the
        // behaviour. Flagged for the owner rather than changed.

        // The user must use the self-service Disable on /account/profile —
        // that requires a current TOTP code as a sanity check, which the
        // admin reset is the very fallback FOR.
        if (target.Id == actorUserId)
        {
            await AuditFailure(actorUserId, request.Email, target.Id,
                ErrorCodes.AdminCannotResetSelf, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminCannotResetSelf, 400,
                "An administrator cannot reset their own 2FA from this page. "
                + "Use the profile page or the operator-level reset.",
                "لا يمكن للمسؤول إعادة تعيين المصادقة الثنائية الخاصة به من هنا. "
                + "استخدم صفحة الملف الشخصي أو إعادة التعيين على مستوى المشغّل.");
        }

        // Administrator vs Administrator is out of scope — those go through
        // the seeder re-pair path. Stops one admin from neutralising another.
        if (await accounts.IsInRoleAsync(target, AdministratorRole))
        {
            await AuditFailure(actorUserId, request.Email, target.Id,
                ErrorCodes.AdminCannotResetAdministrator, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminCannotResetAdministrator, 400,
                "An administrator's 2FA cannot be reset by another administrator. "
                + "The super-administrator's secret is re-paired through configuration.",
                "لا يمكن إعادة تعيين المصادقة الثنائية لمسؤول آخر من خلال هذه الصفحة. "
                + "يتم إعادة ربط سرّ المسؤول الأعلى عبر الإعدادات.");
        }

        var now = timeProvider.SimfNow();

        // The wipe — mirrors TotpEnrollmentService.DisableAsync but skips the
        // "you must prove a current code" gate, by design.
        await accounts.SetTwoFactorEnabledAsync(target, false).EnsureSuccessAsync();
        await accounts.RemoveAuthenticationTokenAsync(
            target, AuthenticatorProvider, ActiveSecretTokenName).EnsureSuccessAsync();
        await accounts.RemoveAuthenticationTokenAsync(
            target, PendingSecretProvider, PendingSecretTokenName).EnsureSuccessAsync();
        target.LastUsedTotpTimestep = null;
        target.UpdatedAt = now;
        await accounts.UpdateAsync(target).EnsureSuccessAsync();
        await recoveryCodes.RevokeAllAsync(target.Id, cancellationToken);

        // Kill every other session the target has open.
        await accounts.UpdateSecurityStampAsync(target);
        await refreshTokenRepository.RevokeAllForUserAsync(target.Id, now, cancellationToken);

        // In-app notification + email (replaces the
        // inline EnqueueNotificationEmail call below). The dispatcher
        // writes the in-app row + queues the rendered email.
        var resetTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = target.DisplayName,
            ["Reason"] = request.Reason,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = target.Id,
            Kind = NotificationKind.AccountTwoFactorReset,
            Title = "Two-factor authentication was reset",
            TitleArabic = "تمت إعادة تعيين المصادقة الثنائية",
            Body = $"An administrator reset 2FA on your account. Reason: {request.Reason}",
            BodyArabic = $"قام أحد المسؤولين بإعادة تعيين المصادقة الثنائية. السبب: {request.Reason}",
            Severity = NotificationSeverity.Warning,
            SendEmail = true,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountTwoFactorReset, "en", resetTokens),
        }, cancellationToken);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminTwoFactorReset,
                Outcome = AuditOutcome.Success,
                SubjectEmail = target.Email,
                SubjectUserId = target.Id,
                ActorUserId = actorUserId,
                Detail = request.Reason,
            },
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} reset 2FA on {TargetEmail} — reason: {Reason}",
            actorUserId, target.Email, request.Reason);
    }

    // -- Admin / Other / Visitor create dispatch -----------------------------

    public Task<AdminCreateUserResponse> CreateAdminAsync(
        Guid actorUserId,
        AdminCreateAdminRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAccountAsync(
            actorUserId, request.Email, request.DisplayName,
            UserType.Admin, profileTypeId: null,
            roles: request.Roles, cancellationToken);

    public Task<AdminCreateUserResponse> CreateOtherAsync(
        Guid actorUserId,
        AdminCreateOtherRequest request,
        CancellationToken cancellationToken = default) =>
        // Other accounts are now Visitor-typed under the hood;
        // the partner-side ProfileType (IsVisitor=false) carries the
        // queue routing. expectedIsVisitor:false enforces the ProfileType
        // belongs to the partner scope so a request with an audience
        // ProfileTypeId is rejected at CreateAccountAsync.
        CreateAccountAsync(
            actorUserId, request.Email, request.DisplayName,
            UserType.Visitor, profileTypeId: request.ProfileTypeId,
            roles: Array.Empty<string>(), cancellationToken,
            expectedIsVisitor: false);

    public Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default) =>
        // When a ProfileTypeId is supplied, enforce that it is
        // audience-side. The Visitor endpoint accepts null
        // ProfileTypeId (tier optional at create time) — the guard
        // only kicks in when a ProfileTypeId is present.
        CreateAccountAsync(
            actorUserId, request.Email, request.DisplayName,
            UserType.Visitor, profileTypeId: request.ProfileTypeId,
            roles: Array.Empty<string>(), cancellationToken,
            expectedIsVisitor: true);

    // ---------------------------------------------------------------------
    // On-site walk-in registration. The CP
    // /admin/visitors and /admin/others pages are registration desks at the
    // event; staff fill the profile in-hand. One transaction creates the user
    // + the profile + the interests. The desk no longer auto-approves:
    // the account lands PendingApproval with NO QR — an admin approves it
    // from the pending queue, which mints the QR badge (the approve path in
    // AdminAccountService.Approval.cs). No password (the QR is the access key,
    // granted on approval).
    // ---------------------------------------------------------------------

    public async Task<AdminWalkInRegistrationResponse> RegisterOnSiteAsync(
        Guid actorUserId,
        UserType kind,
        AdminWalkInRegistrationRequest request,
        CancellationToken cancellationToken = default,
        bool? expectedIsVisitor = null,
        string? presetQrId = null,
        Guid presetProfileId = default)
    {
        // Walk-in registration always creates a Visitor-typed
        // account. The `kind` argument stays on the signature for
        // backward-compat at the endpoint layer but only rejects Admin
        // walk-ins. `expectedIsVisitor` re-introduces the
        // audience-vs-partner desk-URL guard that an earlier cut
        // dropped — the Visitors desk endpoint
        // passes true, the Others desk endpoint passes false, and a
        // desk that picks the wrong-scope ProfileType is rejected
        // with AdminProfileTypeInvalid instead of silently routing the
        // account to the wrong queue.
        if (kind == UserType.Admin)
        {
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                "Walk-in registration is not available for Admin accounts.",
                "التسجيل الفوري غير متاح لحسابات المسؤولين.");
        }

        // Resolve the profile type up-front so we can fail fast + return
        // the colour / name on the success response.
        var profileType = await appDbContext.ProfileTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == request.ProfileTypeId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                "The selected profile type is not valid.",
                "نوع الملف الشخصي المحدّد غير صالح.");
        if (!profileType.IsActive)
        {
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                "The selected profile type is not active or does not apply.",
                "نوع الملف الشخصي المحدّد غير نشط أو غير منطبق.");
        }
        if (expectedIsVisitor is { } expected
            && profileType.IsForVisitor != expected)
        {
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                expected
                    ? "The selected profile type belongs to the partner queue; use the Others walk-in desk."
                    : "The selected profile type belongs to the audience queue; use the Visitors walk-in desk.",
                expected
                    ? "نوع الملف المختار من قائمة الشركاء؛ استخدم نافذة تسجيل الآخرين."
                    : "نوع الملف المختار من قائمة الجمهور؛ استخدم نافذة تسجيل الزوار.");
        }

        // Email is optional for walk-ins; synthesize a placeholder so
        // ASP.NET Identity still has something to anchor the row to.
        // The pattern stays the same as the unique-key contract — Identity
        // needs a unique Email + UserName.
        var providedEmail = (request.Email ?? string.Empty).Trim();
        var hasRealEmail = providedEmail.Length > 0;
        var email = hasRealEmail
            ? providedEmail
            : $"walkin-{Guid.NewGuid():N}@simf.local";

        if (hasRealEmail && await accounts.FindByEmailAsync(email) is not null)
        {
            await AuditFailure(
                AuditEvents.AdminWalkInRegisterFailed, actorUserId, email, null,
                ErrorCodes.AdminEmailAlreadyRegistered, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminEmailAlreadyRegistered, 409,
                "An account with this email already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        // Validate the interest ids the desk picked. Same active-only
        // policy the visitor self-service flow uses.
        var requestedInterests = request.InterestIds.Distinct().ToList();
        var resolvedInterests = requestedInterests.Count == 0
            ? new List<UserInterest>()
            : await appDbContext.Interests
                .Where(i => requestedInterests.Contains(i.Id) && i.IsActive)
                .ToListAsync(cancellationToken);
        if (resolvedInterests.Count != requestedInterests.Count)
        {
            throw new ApiException(
                ErrorCodes.InterestInvalid, 400,
                "One or more selected interests are unknown or no longer active.",
                "بعض الاهتمامات المختارة غير معروفة أو لم تعد مفعّلة.");
        }

        // Quick register. The desk validator's PRESENCE checks moved here
        // so the reduced field set can be allowed only when the mode is armed:
        // FluentValidation is synchronous and FastEndpoints validators are
        // singletons, so the mode cannot be read inside the validator, and a
        // request flag would let any caller with the permission opt themselves
        // out of validation at will. Every SHAPE rule stays in the validator.
        //
        // With the mode disarmed, EnsureFullDeskFields reproduces the validator's
        // original checks with their exact bilingual messages, so behaviour is
        // byte-identical to before.
        var quickRegister = walkInMode.CurrentValue
            .QuickRegisterActive(timeProvider.SimfNow());
        if (quickRegister)
        {
            EnsureQuickDeskFloor(
                request,
                walkInMode.CurrentValue.QuickRegisterRequiresIdentityDocument);
        }
        else
        {
            EnsureFullDeskFields(request);
        }

        // Resolve the wire-side ISO code to the Country PK.
        // Rejected here (400) before any Identity row is created so we
        // never leak a dangling SimfUser for a stranger nationality.
        //
        // In quick mode the code may be omitted, in which case
        // NationalityId falls back to 0. That is the documented "no nationality
        // chosen" value (UserProfileConfiguration) and is what bulk-badge
        // placeholders already write. A code that IS supplied is still resolved
        // and still rejected if unknown.
        var nationalityCode = (request.NationalityCode ?? string.Empty).Trim().ToUpperInvariant();
        var nationalityId = 0;
        var nationalityIsInvited = false;
        if (nationalityCode.Length > 0)
        {
            var nationality = await appDbContext.Countries
                .AsNoTracking()
                .Where(country => country.Code == nationalityCode && country.IsActive)
                .Select(country => new { country.Id, country.IsInvited })
                .SingleOrDefaultAsync(cancellationToken);
            if (nationality is null)
            {
                throw new ApiException(
                    ErrorCodes.ProfileNationalityUnknown, 400,
                    $"Nationality code '{nationalityCode}' is not supported.",
                    $"الجنسية '{nationalityCode}' غير مدعومة.");
            }
            nationalityId = nationality.Id;
            nationalityIsInvited = nationality.IsInvited;
        }
        // A delegate's nationality must be a country invited to
        // send a delegation (وفد). Unchanged by quick mode: a delegate always
        // needs a nationality, because the invited-country rule is what the
        // delegation programme is built on.
        if (request.IsDelegate && !nationalityIsInvited)
        {
            throw new ApiException(
                ErrorCodes.DelegateCountryNotInvited, 400,
                "A delegate's nationality must be a country invited to send a delegation.",
                "يجب أن تكون جنسية عضو الوفد من دولة مدعوّة لإرسال وفد.");
        }

        // Organisation (الجهة): confirm the id resolves to an active Organisation
        // before creating any Identity row, so a bad id surfaces as a clean 400
        // instead of a later FK violation. It is optional in quick mode; the
        // column and its FK are nullable, and profile stubs already leave it null.
        Guid? organisationId = null;
        if (request.OrganisationId is { } requestedOrganisationId
            && requestedOrganisationId != Guid.Empty)
        {
            var organisationIsActive = await appDbContext.Organisations
                .AsNoTracking()
                .AnyAsync(
                    o => o.Id == requestedOrganisationId && o.IsActive, cancellationToken);
            if (!organisationIsActive)
            {
                throw new ApiException(
                    ErrorCodes.OrganisationInvalid, 400,
                    "The selected organisation is not valid.",
                    "الجهة المحددة غير صالحة.");
            }
            organisationId = requestedOrganisationId;
        }

        // On-site duplicate-identity guard (soft, service-layer). A National
        // ID / Iqama / passport already on a profile row must not be re-registered
        // at the desk. The plaintext id columns are AES-GCM encrypted with a RANDOM
        // nonce (SimfAppDbContext), so they can neither be equality-queried nor
        // unique-indexed — the guard + its filtered UNIQUE indexes key off the
        // deterministic blind-index HMAC (pii.BlindIndex) instead. This is a
        // plain single-context read on appDbContext — no cross-DB JOIN. The
        // validator forces two SEPARATE patterns — ^1[0-9]{9}$ (National ID) and
        // ^2[0-9]{9}$ (Iqama) — so IsSaudi partitions the identifiers: at most one
        // is non-null per request. Reads never crash on pre-existing data; a
        // duplicate simply makes the guard match and rejects the new attempt.
        var nationalId = request.IsSaudi ? NormaliseOptional(request.NationalId) : null;
        var iqamaNumber = request.IsSaudi ? null : NormaliseOptional(request.IqamaNumber);
        var passportNumber = request.IsSaudi ? null : NormaliseOptional(request.PassportNumber);
        var nationalIdHash = pii.BlindIndex(nationalId);
        var iqamaNumberHash = pii.BlindIndex(iqamaNumber);
        var passportNumberHash = pii.BlindIndex(passportNumber);

        // Reuse the repository's single OR-query identity-exists check
        // (IUserProfileRepository.AnyOtherProfileWithIdentityHashAsync). Guid.Empty
        // excludes nobody — no real profile has UserId == Guid.Empty — so this NEW
        // walk-in user is checked against every existing profile.
        var duplicateIdentity = await profiles.AnyOtherProfileWithIdentityHashAsync(
            Guid.Empty, nationalIdHash, iqamaNumberHash, passportNumberHash, cancellationToken);
        if (duplicateIdentity)
        {
            await AuditFailure(
                AuditEvents.AdminWalkInRegisterFailed, actorUserId, email, null,
                ErrorCodes.DuplicateIdentity, cancellationToken);
            throw ApiException.DuplicateIdentity();
        }

        var now = timeProvider.SimfNow();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? (string.IsNullOrEmpty(request.EnglishName) ? "Walk-in" : request.EnglishName)
                : request.DisplayName,
            // The walk-in desk creates a PENDING account, not an
            // auto-approved one. An admin approves it from the
            // pending queue, which mints the QR badge. No password —
            // the QR (minted on approval) is the access key.
            AccountState = AccountState.PendingApproval,
            UserType = kind,
            PasswordChangeRequired = false,
            CreatedAt = now,
            StateChangedAt = now,
            StateChangedByUserId = actorUserId,
        };

        var createResult = await accounts.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            await AuditFailure(
                AuditEvents.AdminWalkInRegisterFailed, actorUserId, email, null,
                ErrorCodes.InternalError, cancellationToken,
                detail: string.Join("; ", createResult.Errors.Select(e => e.Description)));
            throw new ApiException(
                ErrorCodes.InternalError, 500,
                "The account could not be created.",
                "تعذّر إنشاء الحساب.");
        }

        // UserProfile.Name and .NameArabic are both NOT NULL, but quick
        // register accepts a name in ONE script. Mirror whichever was captured
        // into the other column (falling back to the display name) so the row is
        // valid and a gate operator always has something to read off the badge.
        // With the full desk both are present and this is a no-op.
        var arabicName = (request.ArabicName ?? string.Empty).Trim();
        var englishName = (request.EnglishName ?? string.Empty).Trim();
        var fallbackName = (request.DisplayName ?? string.Empty).Trim();
        if (arabicName.Length == 0)
        {
            arabicName = englishName.Length > 0 ? englishName : fallbackName;
        }
        if (englishName.Length == 0)
        {
            englishName = arabicName.Length > 0 ? arabicName : fallbackName;
        }

        // Build the profile row with every captured field.
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileType.Id,
            NameArabic = arabicName,
            Name = englishName,
            JobTitle = NormaliseOptional(request.JobTitle),
            JobTitleArabic = NormaliseOptional(request.JobTitleArabic),
            // VVIP/VIP موج extras; null for non-VIP walk-ins (the
            // regular desk form never sends them). The separate VIP photo is
            // uploaded after create via /admin/visitors/{id}/vip-photo.
            MawjId = NormaliseOptional(request.MawjId),
            Honorific = NormaliseOptional(request.Honorific),
            HonorificArabic = NormaliseOptional(request.HonorificArabic),
            PreferredLanguage = NormaliseOptional(request.PreferredLanguage),
            NationalityId = nationalityId,
            DateOfBirth = request.DateOfBirth,
            PlaceOfBirth = (request.PlaceOfBirth ?? string.Empty).Trim(),
            // Gender + plate captured at the walk-in desk (columns
            // already exist on UserProfile; the form just didn't send them).
            Gender = request.Gender,
            // Store the canonical Latin plate code, exactly like
            // the self-service path (UserProfileService.NormalisePlate). A plain
            // trim left an Arabic-script / spaced desk-entered plate stored
            // un-canonicalized, breaking the "one canonical code, both renderings
            // derived on read" invariant + the badge/gate/export key.
            PlateNumber = SaudiPlate.Normalize(request.PlateNumber),
            IsSaudi = request.IsSaudi,
            // Neither the identity documents nor the mobile are set here. The
            // documents live in ProfileIdentityDocuments, written by
            // ProfileIdentityStorage.SyncDocuments; the mobile is written by
            // ProfileMobileStorage.Sync, which fills the canonical column and the
            // two it supersedes together. Setting either here as well would be a
            // second copy of a split rule, and two copies are what drift.
            // The desk-required organisation pick (الجهة).
            OrganisationId = organisationId,
            // Delegation-member flag (a delegate is a normal visitor).
            IsDelegate = request.IsDelegate,
            CreatedAt = now,
        };
        // The canonical number plus the two columns it supersedes, written
        // together and by the SAME helper the self-service upsert uses, so a
        // desk-typed "+966-55 598 7654" and an app-typed "0555987654" land as one
        // string. Deliberately after the initializer rather than inside it: three
        // columns from two inputs is a rule, not three assignments.
        ProfileMobileStorage.Sync(
            profile, request.SaudiMobile, request.InternationalMobile);
        // Visitor kind owns interests; Other kind ignores them per the prompt.
        if (kind == UserType.Visitor)
        {
            foreach (var interest in resolvedInterests)
            {
                profile.Interests.Add(interest);
            }
        }
        // The offline badge upload path. The desk printed this badge
        // without a network, so its QR id is DERIVED from the sequence already
        // encrypted into the paper rather than minted here. Set before the
        // insert: the minter on the approval path is mint-if-missing, so it
        // leaves a populated id alone, and the column's UNIQUE constraint is what
        // makes a repeated upload of the same batch a clean conflict instead of
        // a second account.
        if (!string.IsNullOrEmpty(presetQrId))
        {
            profile.QrId = presetQrId;
        }
        // The same reasoning applies to the id itself. The badge the desk
        // printed carries this Guid, so creating the record under any other one
        // would leave a badge that decrypts perfectly and resolves to nobody.
        if (presetProfileId != Guid.Empty)
        {
            profile.Id = presetProfileId;
        }
        // The three captured numbers, written to the only storage that holds
        // them: one row per document, one unique digest index over all of them,
        // which is what makes a CROSS-KIND duplicate visible at all. The
        // already-normalised values are reused rather than re-derived, so the rows
        // and the soft guard above key off the same strings — a trailing-space
        // passport cannot slip past one and be stored by the other.
        ProfileIdentityStorage.SyncDocuments(
            profile, pii, nationalId, iqamaNumber, passportNumber);
        appDbContext.UserProfiles.Add(profile);

        // No QR at create — the account is PendingApproval; the approve
        // path mints the QR badge. The QR is the access key, granted on
        // approval, not at the desk.
        // UserProfile lives on App DB now; save both contexts.
        // The soft guard above is a non-atomic read-then-insert; a
        // concurrent duplicate that slips it hits the filtered UNIQUE identity
        // index here. Translate that race into the same 409 DuplicateIdentity
        // instead of an uncaught 500 (narrow — any other violation rethrows).
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        // ONE index name now, where there used to be three per-kind ones beside
        // it: the child table's single digest index is the whole duplicate-identity
        // constraint, and it fires on a cross-kind duplicate the three could not
        // see. Named from the constant, not a string, so removing the index cannot
        // leave a filter that silently stops matching and turns this 409 into a
        // 500. Deliberately NOT added to the QrId catch below, which answers a
        // different conflict with a different code.
        catch (DbUpdateException ex) when (ex.ViolatesAnyIndex(
            Persistence.Configurations.App.ProfileIdentityDocumentConfiguration
                .NumberHashIndexName))
        {
            await AuditFailure(
                AuditEvents.AdminWalkInRegisterFailed, actorUserId, email, null,
                ErrorCodes.DuplicateIdentity, cancellationToken);
            throw ApiException.DuplicateIdentity();
        }
        // Two desks uploading the same batch at once. The pre-check in
        // the upload service is a non-atomic read-then-insert, so the loser lands
        // here; translate it into the same "already uploaded" answer the
        // pre-check gives rather than a 500, and the retry stays idempotent.
        catch (DbUpdateException ex) when (
            !string.IsNullOrEmpty(presetQrId)
            && ex.ViolatesAnyIndex("IX_UserProfiles_QrId"))
        {
            await AuditFailure(
                AuditEvents.AdminWalkInRegisterFailed, actorUserId, email, null,
                ErrorCodes.OfflineBadgeSequenceTaken, cancellationToken);
            throw new ApiException(
                ErrorCodes.OfflineBadgeSequenceTaken, 409,
                "This badge sequence has already been uploaded.",
                "تم رفع هذا الرقم التسلسلي للبطاقة من قبل.");
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // Record WHICH fields a quick registration omitted, so the
        // incomplete profiles can be chased and completed after the event. The
        // CP visitor edit page and the attendee's own profile save already fill
        // them in; this is the list of who to chase.
        if (quickRegister)
        {
            var omitted = new List<string>(4);
            if (nationalityId == 0) { omitted.Add("nationality"); }
            if (organisationId is null) { omitted.Add("organisation"); }
            if (string.IsNullOrWhiteSpace(request.SaudiMobile)
                && string.IsNullOrWhiteSpace(request.InternationalMobile))
            {
                omitted.Add("mobile");
            }
            if (nationalId is null && iqamaNumber is null && passportNumber is null)
            {
                omitted.Add("identityDocument");
            }

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.AdminQuickRegistered,
                Outcome = AuditOutcome.Success,
                ActorUserId = actorUserId,
                SubjectUserId = user.Id,
                SubjectEmail = email,
                Detail = omitted.Count == 0
                    ? "omitted=none"
                    : "omitted=" + string.Join(",", omitted),
            }, cancellationToken);
        }

        // Walk-in auto-approval. Approval is what MINTS THE QR, so
        // without this a walk-in leaves the desk with no badge and the main gate
        // correctly refuses them. This is the switch that makes the offline desk
        // and session walk-in usable.
        //
        // Reuses the one approval path rather than writing a second: at this
        // point the account is exactly the PendingApproval state ApproveAsync
        // expects, so the call inherits the QR mint, the App-then-Identity write
        // ordering, token revocation, the audit row and the notification. A
        // parallel implementation would have to keep all five in step.
        //
        // AUDIENCE VISITORS ONLY. A partner / "Other" profile type can carry an
        // operational MobileAppRole (Staff, Moderator, Exhibitor) and approval is
        // exactly what activates it, so auto-approving that desk would hand out
        // staff powers with nobody reviewing. Same rule bulk badge generation
        // already enforces.
        if (expectedIsVisitor == true
            && walkInMode.CurrentValue.AutoApproveActive(timeProvider.SimfNow()))
        {
            try
            {
                await ApproveAsync(
                    actorUserId, user.Id, ApprovalScope.AudienceVisitor,
                    cancellationToken, profileTypeId: null,
                    sendApprovalEmail: hasRealEmail);

                // Re-read so the response carries the freshly minted QR.
                profile.QrId = await appDbContext.UserProfiles
                    .AsNoTracking()
                    .Where(p => p.UserId == user.Id)
                    .Select(p => p.QrId)
                    .SingleOrDefaultAsync(cancellationToken);

                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminVisitorAutoApproved,
                    Outcome = AuditOutcome.Success,
                    ActorUserId = actorUserId,
                    SubjectUserId = user.Id,
                    SubjectEmail = email,
                    Detail = $"qrId={profile.QrId}; profileType={profileType.Name}; "
                        + "reason=walkInMode",
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // The visitor IS registered and sits in the normal pending queue.
                // A failed auto-approve must never lose a registration during a
                // rush, so this degrades to today's behaviour: the desk falls
                // back to a paper slip while an admin approves from the queue.
                //
                // The QR is cleared from the RESPONSE explicitly.
                // ApproveAsync saves the App DB (minting the QR onto this same
                // tracked profile instance) before it flips Identity, so a
                // failure in the second half leaves a real, persisted QrId on an
                // account that is still PendingApproval. Returning it would have
                // the desk print a badge the gate then refuses as
                // HolderNotApproved. Access stays fail-closed either way; this
                // keeps the desk from printing paper it cannot use.
                profile.QrId = null;
                logger.LogError(
                    ex,
                    "Walk-in auto-approve failed for {UserId}; left PendingApproval.",
                    user.Id);
                try
                {
                    await AuditFailure(
                        AuditEvents.AdminVisitorAutoApproveFailed, actorUserId, email,
                        user.Id, ErrorCodes.InternalError, cancellationToken);
                }
                catch (Exception auditFailure)
                {
                    // The likeliest cause of the approval failure is the database
                    // itself, in which case this audit write fails too. Losing the
                    // audit row is bad; throwing here would lose the operator's
                    // whole response for a registration that DID commit, which is
                    // worse.
                    logger.LogError(
                        auditFailure,
                        "Could not audit the failed auto-approve for {UserId}.",
                        user.Id);
                }
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminWalkInRegistered,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            SubjectEmail = email,
            // Include profileTypeIsVisitor so SOC can bucket walk-in
            // bursts by audience vs partner desk (kind is always
            // Visitor now, so it no longer distinguishes; the desk URL
            // is reflected via the expectedIsVisitor parameter the
            // endpoint passes in).
            Detail = $"kind={kind}; profileType={profileType.Name}; "
                + $"profileTypeIsVisitor={profileType.IsForVisitor}; "
                + $"hasEmail={hasRealEmail}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} walk-in registered {Kind} {Email} (PendingApproval; QR minted on approval)",
            actorUserId, kind, email);

        return new AdminWalkInRegistrationResponse(
            user.Id,
            email,
            user.DisplayName,
            profile.QrId ?? string.Empty,
            profileType.Name,
            profileType.NameArabic,
            profileType.PageColor);
    }

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Shared back-end of every create call. Routes a
    /// <see cref="UserType"/> + optional <c>ProfileTypeId</c> + optional
    /// RBAC role grants through one create + invite + audit pipeline.
    /// </summary>
    private async Task<AdminCreateUserResponse> CreateAccountAsync(
        Guid actorUserId,
        string email,
        string displayName,
        UserType userType,
        Guid? profileTypeId,
        IList<string> roles,
        CancellationToken cancellationToken,
        bool? expectedIsVisitor = null)
    {
        if (await accounts.FindByEmailAsync(email) is not null)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, email, null,
                ErrorCodes.AdminEmailAlreadyRegistered, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminEmailAlreadyRegistered, 409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        // Validate the ProfileTypeId before creating the user so
        // the row + the FK land atomically. Also enforce that
        // profileType.IsVisitor matches the
        // caller's expectedIsVisitor flag — without this guard, the
        // /admin/others/* family would accept an audience ProfileType
        // (and vice versa) and the resulting account would land on the
        // wrong CP queue with the wrong audit-event mapping.
        if (profileTypeId is { } id)
        {
            var profileType = await appDbContext.ProfileTypes
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (profileType is null || !profileType.IsActive)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type is not valid or no longer active.",
                    "نوع الملف الشخصي المحدّد غير صالح أو لم يعد مفعّلاً.");
            }
            if (userType != UserType.Visitor)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type does not apply to this user type.",
                    "نوع الملف الشخصي المحدّد لا ينطبق على هذا النوع من المستخدمين.");
            }
            if (expectedIsVisitor is { } expected
                && profileType.IsForVisitor != expected)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    expected
                        ? "The selected profile type is partner-side; use the Others endpoint to assign it."
                        : "The selected profile type is audience-side; use the Visitors endpoint to assign it.",
                    expected
                        ? "نوع الملف المختار من نطاق الشركاء؛ استخدم نقطة نهاية الآخرين لتعيينه."
                        : "نوع الملف المختار من نطاق الجمهور؛ استخدم نقطة نهاية الزوار لتعيينه.");
            }
        }

        var now = timeProvider.SimfNow();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            // Created users land in PendingApproval; the QR id is minted
            // on approval.
            AccountState = AccountState.PendingApproval,
            UserType = userType,
            // ProfileTypeId no longer lives on SimfUser; if the
            // admin picked one we create a stub UserProfile row below
            // so the FK has somewhere to land at create time.
            PasswordChangeRequired = false,
            CreatedAt = now,
        };
        // No password yet — the new user sets it via the invite link.
        var createResult = await accounts.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, email, null,
                ErrorCodes.InternalError, cancellationToken,
                detail: string.Join("; ", createResult.Errors.Select(error => error.Description)));
            throw new ApiException(
                ErrorCodes.InternalError, 500,
                "The account could not be created.",
                "تعذّر إنشاء الحساب.");
        }

        // A CP-provisioned admin must never end up permanently single-factor,
        // so the flag is set at creation rather than left to the admin's own
        // choice on /account/profile.
        //
        // The condition expresses the dependency on the enrolment-first sign-in
        // branch in code rather than as a note in a plan. A new admin has a
        // role and no authenticator
        // key, and the factor selector in SignInService picks
        // `key != "" || roles.Count > 0 ? Totp : EmailOtp` — so setting this flag
        // on its own challenges every new admin for a TOTP code against a secret
        // that does not exist and locks them out at creation. The
        // enrolment-first branch is what hands them a way in, and it runs under
        // exactly this setting: when the enrolment path is switched off, forcing
        // the flag would be a lockout, so we do not force it.
        if (userType == UserType.Admin
            && lifecycleOptions.Value.RequireControlPanelTwoFactorEnrolment)
        {
            await accounts.SetTwoFactorEnabledAsync(user, true).EnsureSuccessAsync();
        }

        // RBAC roles are valid only for Admin-typed users.
        if (userType == UserType.Admin && roles.Count > 0)
        {
            foreach (var role in roles)
            {
                if (await roleManager.RoleExistsAsync(role))
                {
                    await accounts.AddToRoleAsync(user, role).EnsureSuccessAsync();
                }
            }
        }

        // If the admin picked a ProfileTypeId we drop a stub
        // UserProfile row so the FK has somewhere to land. The user fills
        // the rest of the form later via /account/profile. Admins never
        // carry a profile so we never stub for them.
        if (profileTypeId is { } chosenProfileTypeId && userType != UserType.Admin)
        {
            appDbContext.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = chosenProfileTypeId,
                CreatedAt = now,
            });
            // Profile-stub lands on the App DB.
            await appDbContext.SaveChangesAsync(cancellationToken);
        }

        // 7-day invite.
        var inviteLifetime = TimeSpan.FromDays(7);
        // M3 (security) — store only the keyed hash; email the plaintext invite.
        var plaintext = VerificationCodeGenerator.Generate();
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.PasswordReset,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(inviteLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        EnqueueInviteEmail(user.Email!, user.DisplayName, plaintext, inviteLifetime);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminUserCreated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                ActorUserId = actorUserId,
                Detail = $"userType={userType}; roles={string.Join(",", roles)}",
            },
            cancellationToken);

        // In-app welcome row for the new user — visible the first
        // time they sign in. SendEmail=false because the invite email
        // (sent just above) already greets them with the code; a second
        // welcome email would duplicate.
        var welcomeTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = user.DisplayName ?? user.Email ?? string.Empty,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.AccountWelcome,
            Title = "Welcome to SIMF",
            TitleArabic = "مرحباً بك في SIMF",
            Body = "Your SIMF account has been created. Check your email to set your password, then sign in.",
            BodyArabic = "تم إنشاء حسابك في SIMF. تحقق من بريدك لتعيين كلمة المرور، ثم سجّل الدخول.",
            Severity = NotificationSeverity.Success,
            SendEmail = false,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountWelcome, "en", welcomeTokens),
        }, cancellationToken);

        // Fan-out AdminPendingApproval to every other Approved
        // Administrator — the actor admin who just clicked Create is
        // excluded to avoid self-pinging. Same shape as the visitor
        // self-submit fan-out in UserProfileService.DispatchAdminPendingVisitorAsync.
        var otherAdmins = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Admin
                && u.AccountState == AccountState.Approved
                && u.Id != actorUserId)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(cancellationToken);
        foreach (var admin in otherAdmins)
        {
            var pendingTokens = new Dictionary<string, string>
            {
                ["DisplayName"] = admin.DisplayName ?? string.Empty,
                ["SubjectEmail"] = user.Email ?? string.Empty,
                ["SubjectUserType"] = userType.ToString(),
            };
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = admin.Id,
                Kind = NotificationKind.AdminPendingApproval,
                Title = $"New {userType} awaiting approval — {user.Email}",
                TitleArabic = $"حساب {userType} جديد بانتظار الموافقة — {user.Email}",
                Body = $"A new {userType} account was created and is awaiting approval: {user.Email}.",
                BodyArabic = $"تم إنشاء حساب {userType} جديد بانتظار الموافقة: {user.Email}.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = "User",
                RelatedEntityId = user.Id,
                SendEmail = true,
                PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                    NotificationKind.AdminPendingApproval, "en", pendingTokens),
            }, cancellationToken);
        }

        logger.LogInformation(
            "Admin {ActorId} created {UserType} {Email}",
            actorUserId, userType, user.Email);
        return new AdminCreateUserResponse(
            user.Id, user.Email!, (int)inviteLifetime.TotalSeconds);
    }

    public Task<GridPage<AdminUserSummary>> ListAdminsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListAccountsAsync(query, UserType.Admin, profileScope: null, cancellationToken);

    public Task<GridPage<AdminUserSummary>> ListOthersAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        // Others = Visitor users carrying a partner-side
        // ProfileType (IsVisitor=false). The underlying account is the
        // same Visitor pool — only the linked ProfileType distinguishes.
        ListAccountsAsync(query, UserType.Visitor, profileScope: false, cancellationToken);

    public Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        // Visitors = Visitor users carrying an audience-side
        // ProfileType (IsVisitor=true) OR no ProfileType yet.
        ListAccountsAsync(query, UserType.Visitor, profileScope: true, cancellationToken);

    // The per-family avatar routes gate on this so one
    // View/Edit permission cannot read/overwrite another family's photo across the
    // shared SimfUser id space. Mirrors the list scoping: UserType first, then (for
    // the Visitor family) the audience-vs-partner ProfileType split.
    public async Task<bool> IsSubjectInFamilyAsync(
        Guid userId, UserType expectedType, bool? expectedIsVisitor,
        CancellationToken cancellationToken = default)
    {
        // Step 1 (Identity DB): the account's UserType must match the family.
        var actualType = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (UserType?)u.UserType)
            .FirstOrDefaultAsync(cancellationToken);
        if (actualType != expectedType) { return false; }
        if (expectedIsVisitor is null) { return true; }  // Admin family — type is enough.

        // Step 2 (App DB): narrow the Visitor family to audience vs partner by the
        // linked ProfileType, mirroring ResolveProfileScopedUserIdsAsync (partner =
        // a UserProfile linked to a ProfileType with IsForVisitor == false). Two
        // separate reads across the DB split — never a cross-DB join.
        var isPartner = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ProfileTypeId != null
                && appDbContext.ProfileTypes.Any(pt =>
                    pt.Id == p.ProfileTypeId && !pt.IsForVisitor))
            .AnyAsync(cancellationToken);
        return expectedIsVisitor.Value ? !isPartner : isPartner;
    }

    /// <summary>Shared back-end of every list call. Narrows to one
    /// <see cref="UserType"/> and optionally further by the
    /// linked ProfileType's <c>IsVisitor</c> flag. <paramref name="profileScope"/>:
    /// <c>true</c> = audience side (no profile or IsVisitor=true);
    /// <c>false</c> = partner side (IsVisitor=false); <c>null</c> = no
    /// profile-scope filter (used by the Admins list).</summary>
    private async Task<GridPage<AdminUserSummary>> ListAccountsAsync(
        GridQuery query, UserType userType, bool? profileScope,
        CancellationToken cancellationToken)
    {
        // Normalise: clamp Top to [1..200], clamp Skip to [0..). The grid
        // contract (SIMF.Common.GridQuery) says the endpoint owns the clamp.
        var (skip, top) = query.ClampPage(20, 200);

        // Resolve the Administrator role id once for the per-row "is admin"
        // flag. Only Admin-typed users carry RBAC roles.
        var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);

        // Cross-context scope guard — fetch the user-id set that
        // matches the requested profile scope. SimfUser lives in the
        // Identity DB and UserProfile + ProfileType in the App DB so EF
        // join is not available. The set is small under the current
        // single-event SIMF scale (<2k partner accounts); a future
        // multi-event variant should swap this for a batched contains
        // or replicate the scope flag onto SimfUser.
        var scopedUserIds = await ResolveProfileScopedUserIdsAsync(
            profileScope, cancellationToken);

        // Narrow by UserType. The list is narrowed BEFORE any
        // filter/sort/page so the totals are correct.
        var users = dbContext.Users
            .Where(u => u.UserType == userType);
        if (scopedUserIds is not null)
        {
            users = users.Where(u => scopedUserIds.Contains(u.Id));
        }

        // -- Search ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            // The registration reference (SIMF-YYYY-NNNNNNNN) lives
            // on the App-DB profile row; cross-DB means resolve the matching
            // user ids first (never a JOIN). Capped: a reference
            // search is effectively exact, so the set is tiny.
            var referenceUserIds = await appDbContext.UserProfiles
                .AsNoTracking()
                .Where(p => p.ReferenceNumber != null
                    && EF.Functions.Like(p.ReferenceNumber, $"%{term}%"))
                .Select(p => p.UserId)
                .Take(200)
                .ToListAsync(cancellationToken);
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))
                || EF.Functions.Like(u.DisplayName, $"%{term}%")
                || referenceUserIds.Contains(u.Id));
        }

        // -- Per-column filters --------------------------------------------
        if (query.Filters.TryGetValue("email", out var emailFilter)
            && !string.IsNullOrWhiteSpace(emailFilter))
        {
            users = users.Where(u =>
                u.Email != null && EF.Functions.Like(u.Email, $"%{emailFilter}%"));
        }
        if (query.Filters.TryGetValue("displayName", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            users = users.Where(u => EF.Functions.Like(u.DisplayName, $"%{nameFilter}%"));
        }
        if (query.Filters.TryGetValue("state", out var stateFilter)
            && !string.IsNullOrWhiteSpace(stateFilter)
            && Enum.TryParse<AccountState>(stateFilter, ignoreCase: true, out var parsedState))
        {
            users = users.Where(u => u.AccountState == parsedState);
        }
        if (query.Filters.TryGetValue("twoFactor", out var twoFactorFilter)
            && bool.TryParse(twoFactorFilter, out var twoFactorOn))
        {
            users = users.Where(u => u.TwoFactorEnabled == twoFactorOn);
        }

        // -- Sort -----------------------------------------------------------
        users = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("email", true) => users.OrderByDescending(u => u.Email),
            ("email", false) => users.OrderBy(u => u.Email),
            ("displayname", true) => users.OrderByDescending(u => u.DisplayName),
            ("displayname", false) => users.OrderBy(u => u.DisplayName),
            ("state", true) => users.OrderByDescending(u => u.AccountState),
            ("state", false) => users.OrderBy(u => u.AccountState),
            ("createdat", false) => users.OrderBy(u => u.CreatedAt),
            // Natural order: newest first.
            _ => users.OrderByDescending(u => u.CreatedAt),
        };

        var total = await users.CountAsync(cancellationToken);

        // Per-row role flag — projected inside the EF query.
        var rows = await users
            .Skip(skip)
            .Take(top)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.AccountState,
                user.TwoFactorEnabled,
                user.CreatedAt,
                // Presence sentinel — non-empty when the account has a
                // profile photo (avatar) in the StoredFile store; drives the
                // grid photo thumbnail.
                user.AvatarFileId,
                IsAdmin = adminRoleId != null
                    && dbContext.UserRoles.Any(ur =>
                        ur.UserId == user.Id && ur.RoleId == adminRoleId),
            })
            .ToListAsync(cancellationToken);

        var summaries = rows
            .Select(row => new AdminUserSummary(
                row.Id,
                row.Email ?? string.Empty,
                row.DisplayName,
                row.AccountState.ToString(),
                row.TwoFactorEnabled,
                row.IsAdmin,
                row.CreatedAt,
                HasAvatar: row.AvatarFileId is not null))
            .ToList();

        return GridPage<AdminUserSummary>.Of(summaries, total,
            skip, top);
    }

    private async Task<Guid?> GetAdministratorRoleIdAsync(CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(AdministratorRole);
        return role?.Id;
    }

    /// <summary>Every CP role id present in the database. The set is
    /// (Administrator, Staff, Scientific, Security); missing roles are
    /// dropped silently so the seeder is the single source of role identity.</summary>
    private async Task<IReadOnlyList<Guid>> GetCpRoleIdsAsync(CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(AppRoles.CpRoles.Count);
        foreach (var name in AppRoles.CpRoles)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role is not null) { ids.Add(role.Id); }
        }
        return ids;
    }

    // -- Approval workflow (Admin / Other / Visitor) -------------------------

    public Task ApproveAdminAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.Admin, cancellationToken);

    public Task ApproveOtherAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.PartnerOther, cancellationToken);

    public Task ApproveVisitorAsync(
        Guid actorUserId, Guid subjectUserId, Guid? profileTypeId = null,
        CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.AudienceVisitor,
            cancellationToken, profileTypeId);

    public Task RejectAdminAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        CancellationToken cancellationToken = default) =>
        RejectAsync(actorUserId, subjectUserId, request, ApprovalScope.Admin, cancellationToken);

    public Task RejectOtherAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        CancellationToken cancellationToken = default) =>
        RejectAsync(actorUserId, subjectUserId, request, ApprovalScope.PartnerOther, cancellationToken);

    public Task RejectVisitorAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        CancellationToken cancellationToken = default) =>
        RejectAsync(actorUserId, subjectUserId, request, ApprovalScope.AudienceVisitor, cancellationToken);

    // Approval queue scope. Audience / Partner both back onto
    // UserType.Visitor; only the linked ProfileType.IsVisitor flag
    // distinguishes. Admin is its own queue.
    private enum ApprovalScope { AudienceVisitor, PartnerOther, Admin }

    private static UserType UserTypeOf(ApprovalScope scope) => scope switch
    {
        ApprovalScope.Admin => UserType.Admin,
        _ => UserType.Visitor,
    };

    private static bool? ProfileScopeOf(ApprovalScope scope) => scope switch
    {
        ApprovalScope.AudienceVisitor => true,
        ApprovalScope.PartnerOther => false,
        _ => null,
    };

    // Endpoints still take a legacy UserType parameter
    // (Visitor / Other) for backward-compat URL routing. Map it to
    // the IsVisitor flag the scope guards expect.
    private static bool ProfileScopeFromLegacyKind(UserType kind) =>
        kind == UserType.Visitor;

    // Fetch the set of SimfUser ids that match the requested
    // ProfileType.IsVisitor scope. Returns null when no profile-scope
    // filter is requested (Admin queue). The audience side includes
    // users with no ProfileType yet — a self-signed-up visitor with
    // no admin-assigned tier still lands on the Visitors queue.
    private async Task<HashSet<Guid>?> ResolveProfileScopedUserIdsAsync(
        bool? profileScope, CancellationToken cancellationToken)
    {
        if (profileScope is null) { return null; }
        var requireIsVisitor = profileScope.Value;

        // Partner ProfileTypes are the discriminator. Audience scope is
        // defined as `visitor MINUS partner`, so any Visitor account
        // that isn't explicitly linked to a partner-side ProfileType
        // (no profile row, profile row with null ProfileTypeId, or
        // profile row with an audience ProfileType) lands on the
        // Visitors queue. An earlier implementation used
        // `visitor MINUS withAnyProfile`, which
        // dropped self-signup visitors whose `UserProfileService
        // .UpsertMineAsync` created a profile row with a null
        // ProfileTypeId — they were invisible to BOTH queues.
        var partnerProfileTypeIds = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.IsForVisitor == false)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // This builds a set of ACCOUNT ids to scope an admin's reach, so a
        // profile with no account contributes nothing to it — there is no
        // account for the scope to include or exclude.
        var partnerUserIds = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId != null
                && p.ProfileTypeId != null
                && partnerProfileTypeIds.Contains(p.ProfileTypeId.Value))
            .Select(p => p.UserId!.Value)
            .ToListAsync(cancellationToken);

        if (!requireIsVisitor)
        {
            // Partner scope = exactly the users explicitly linked to a
            // partner ProfileType.
            return new HashSet<Guid>(partnerUserIds);
        }

        // Audience scope = every Visitor-typed user that is NOT in the
        // partner set. Cross-context: enumerate Visitor user-ids in
        // Identity, then subtract the partner set computed against the
        // App DB.
        var visitorIds = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Visitor)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var partnerSet = new HashSet<Guid>(partnerUserIds);
        var audienceSet = new HashSet<Guid>(visitorIds.Count);
        foreach (var id in visitorIds)
        {
            if (!partnerSet.Contains(id))
            {
                audienceSet.Add(id);
            }
        }
        return audienceSet;
    }

    public Task<GridPage<AdminPendingUserSummary>> ListPendingAdminsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListPendingAsync(query, UserType.Admin, profileScope: null, cancellationToken);

    public Task<GridPage<AdminPendingUserSummary>> ListPendingOthersAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListPendingAsync(query, UserType.Visitor, profileScope: false, cancellationToken);

    public Task<GridPage<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListPendingAsync(query, UserType.Visitor, profileScope: true, cancellationToken);

    /// <summary>Pending-approval list narrowed by UserType.
    /// <paramref name="profileScope"/> further narrows the
    /// Visitor scope into the audience (true) and partner (false)
    /// approval queues; null = no profile-scope filter (Admin queue).</summary>
    private async Task<GridPage<AdminPendingUserSummary>> ListPendingAsync(
        GridQuery query, UserType userType, bool? profileScope,
        CancellationToken cancellationToken)
    {
        var (skip, top) = query.ClampPage(20, 200);

        var scopedUserIds = await ResolveProfileScopedUserIdsAsync(
            profileScope, cancellationToken);

        var users = dbContext.Users
            .Where(u => u.AccountState == AccountState.PendingApproval
                && u.UserType == userType);
        if (scopedUserIds is not null)
        {
            users = users.Where(u => scopedUserIds.Contains(u.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))
                || EF.Functions.Like(u.DisplayName, $"%{term}%"));
        }

        // -- Per-column filters (CP grid Filterable columns: email, displayName) --
        if (query.Filters.TryGetValue("email", out var emailFilter)
            && !string.IsNullOrWhiteSpace(emailFilter))
        {
            users = users.Where(u =>
                u.Email != null && EF.Functions.Like(u.Email, $"%{emailFilter}%"));
        }
        if (query.Filters.TryGetValue("displayName", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            users = users.Where(u => EF.Functions.Like(u.DisplayName, $"%{nameFilter}%"));
        }

        // -- Sort (Sortable columns: email, displayName; default newest-first) --
        users = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("email", true) => users.OrderByDescending(u => u.Email),
            ("email", false) => users.OrderBy(u => u.Email),
            ("displayname", true) => users.OrderByDescending(u => u.DisplayName),
            ("displayname", false) => users.OrderBy(u => u.DisplayName),
            // Natural order: newest first (the `created` column is not sortable).
            _ => users.OrderByDescending(u => u.CreatedAt),
        };

        var total = await users.CountAsync(cancellationToken);
        var page = await users
            .Skip(skip).Take(top)
            .Select(u => new AdminPendingUserSummary(
                u.Id, u.Email!, u.DisplayName, u.CreatedAt,
                // != null, not "is not null": this is translated to SQL, and an
                // expression tree cannot carry a pattern match.
                u.AvatarFileId != null))
            .ToListAsync(cancellationToken);

        return GridPage<AdminPendingUserSummary>.Of(page, total,
            skip, top);
    }


    private Task AuditFailure(
        Guid actorUserId, string email, Guid? targetUserId, string errorCode,
        CancellationToken cancellationToken) =>
        AuditFailure(AuditEvents.AdminTwoFactorResetFailed, actorUserId, email,
            targetUserId, errorCode, cancellationToken);

    private Task AuditFailure(
        string eventType, Guid actorUserId, string email, Guid? targetUserId,
        string errorCode, CancellationToken cancellationToken, string? detail = null) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = email,
                SubjectUserId = targetUserId,
                ActorUserId = actorUserId,
                ErrorCode = errorCode,
                Detail = detail,
            },
            cancellationToken);

    private void EnqueueInviteEmail(
        string targetEmail, string displayName, string code, TimeSpan lifetime)
    {
        var days = (int)lifetime.TotalDays;
        var body =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(displayName)},</p>"
            + "<p>A SIMF account has been created for you. To activate it, "
            + "open the Control Panel and set your password with the "
            + "verification code below.</p>"
            + $"<p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(targetEmail)}<br/>"
            + $"<strong>Code:</strong> <strong>{code}</strong><br/>"
            + $"<strong>Valid for:</strong> {days} days.</p>"
            + "<p>If you did not expect this invitation, you can ignore this email.</p>";
        emailQueue.Enqueue(new EmailMessage(
            targetEmail, "SIMF — your account invitation", body));
    }

    private void EnqueueNotificationEmail(string targetEmail, string actorEmail, string reason)
    {
        var body =
            "<p>An administrator has reset the two-factor authentication on your SIMF account.</p>"
            + $"<p><strong>Performed by:</strong> {System.Net.WebUtility.HtmlEncode(actorEmail)}<br/>"
            + $"<strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>"
            + "<p>Your authenticator app and any recovery codes are no longer valid. "
            + "Sign in with your password and set up two-factor authentication again "
            + "from your profile page.</p>"
            + "<p>If you did not request this, contact your security team immediately.</p>";
        emailQueue.Enqueue(new EmailMessage(
            targetEmail, "SIMF — your two-factor authentication was reset", body));
    }

}
