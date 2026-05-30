// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs,
//        SIMF.Api.Tests/AdminCreateUserTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Notifications;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin-driven user-management use cases (decisions D-041, D-042). First
/// slice of the User Management module from <c>myComment</c> #33 — reset
/// 2FA, create a new CP user, list every account.
///
/// <para>R2 — D-075: implements five focused interfaces split out of the
/// pre-R2 monolithic <c>IAdminAccountService</c> (Architecture SEV-1.2).
/// The implementation stays in one class for now (§17 minimum-viable
/// diff — the interface split is the architectural improvement the
/// callers care about); splitting the implementation into five 150-
/// 250-line classes is a follow-up.</para>
/// </summary>
internal sealed class AdminAccountService(
    IUserAccountRepository accounts,
    RoleManager<SimfRole> roleManager,
    IRefreshTokenRepository refreshTokenRepository,
    IRecoveryCodeService recoveryCodes,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    IUserExcelService excel,
    IQrIdMinter qrIdMinter,
    ITransactionRunner transactionRunner,
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    ILogger<AdminAccountService> logger)
    : IAdminTwoFactorService,
      IAdminUserApprovalService,
      IAdminUserProvisioningService,
      IAdminUserBulkService
{
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

        var now = timeProvider.GetUtcNow();

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

        // P13 — D-054: in-app notification + email (replaces the
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

    // -- P7c — Admin / Other / Visitor create dispatch -----------------------

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
        // D-186: Other accounts are now Visitor-typed under the hood;
        // the partner-side ProfileType (IsVisitor=false) carries the
        // queue routing instead. The caller is expected to pass a
        // ProfileTypeId whose ProfileType.IsVisitor=false — admin walk-in
        // / create flows enforce this server-side at CreateAccountAsync.
        CreateAccountAsync(
            actorUserId, request.Email, request.DisplayName,
            UserType.Visitor, profileTypeId: request.ProfileTypeId,
            roles: Array.Empty<string>(), cancellationToken);

    public Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAccountAsync(
            actorUserId, request.Email, request.DisplayName,
            UserType.Visitor, profileTypeId: request.ProfileTypeId,
            roles: Array.Empty<string>(), cancellationToken);

    // ---------------------------------------------------------------------
    // D-127 — on-site walk-in registration. The CP /admin/visitors and
    // /admin/others pages are registration desks at the event; staff
    // verify the person in-hand and the user is auto-approved (no pending
    // queue, no invite email). One transaction creates the user + the
    // profile + the interests + mints the QR; the response carries the QR
    // so the desk can immediately show or print the badge.
    // ---------------------------------------------------------------------

    public async Task<AdminWalkInRegistrationResponse> RegisterOnSiteAsync(
        Guid actorUserId,
        UserType kind,
        AdminWalkInRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        // D-186: walk-in registration always creates a Visitor-typed
        // account. The audience-vs-partner split lives entirely on the
        // chosen ProfileType.IsVisitor — the legacy `kind` argument is
        // still accepted for endpoint-shape stability but only rejects
        // Admin walk-ins.
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
        // D-186: the chosen ProfileType must belong to the Visitor
        // scope and be active. Audience-vs-partner is the desk's
        // pick on the CP; the server trusts the chosen ProfileType.
        if (!profileType.IsActive || profileType.UserType != UserType.Visitor)
        {
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                "The selected profile type is not active or does not apply.",
                "نوع الملف الشخصي المحدّد غير نشط أو غير منطبق.");
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
            ? new List<Interest>()
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

        // D-151 — resolve the wire-side ISO code to the Country PK.
        // Rejected here (400) before any Identity row is created so we
        // never leak a dangling SimfUser for a stranger nationality.
        var nationalityCode = (request.NationalityCode ?? string.Empty).Trim().ToUpperInvariant();
        var nationalityId = await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Code == nationalityCode && country.IsActive)
            .Select(country => (int?)country.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (nationalityId is null)
        {
            throw new ApiException(
                ErrorCodes.ProfileNationalityUnknown, 400,
                $"Nationality code '{nationalityCode}' is not supported.",
                $"الجنسية '{nationalityCode}' غير مدعومة.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? (string.IsNullOrEmpty(request.EnglishName) ? "Walk-in" : request.EnglishName)
                : request.DisplayName,
            // Auto-approve — staff has verified the person face-to-face.
            AccountState = AccountState.Approved,
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

        // Build the profile row with every captured field.
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileType.Id,
            ArabicName = (request.ArabicName ?? string.Empty).Trim(),
            EnglishName = (request.EnglishName ?? string.Empty).Trim(),
            JobTitle = NormaliseOptional(request.JobTitle),
            NationalityId = nationalityId.Value,
            DateOfBirth = request.DateOfBirth,
            PlaceOfBirth = (request.PlaceOfBirth ?? string.Empty).Trim(),
            IsSaudi = request.IsSaudi,
            NationalId = request.IsSaudi ? request.NationalId : null,
            IqamaNumber = request.IsSaudi ? null : request.IqamaNumber,
            PassportNumber = request.IsSaudi ? null : request.PassportNumber,
            SaudiMobile = NormaliseOptional(request.SaudiMobile),
            InternationalMobile = NormaliseOptional(request.InternationalMobile),
            CreatedAt = now,
        };
        // Visitor kind owns interests; Other kind ignores them per the prompt.
        if (kind == UserType.Visitor)
        {
            foreach (var interest in resolvedInterests)
            {
                profile.Interests.Add(interest);
            }
        }
        appDbContext.UserProfiles.Add(profile);

        // Mint the QR badge synchronously — the response carries it so
        // the desk can render the badge before the visitor walks away.
        await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);
        // D-167: UserProfile lives on App DB now; save both contexts.
        await appDbContext.SaveChangesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminWalkInRegistered,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            SubjectEmail = email,
            Detail = $"kind={kind}; profileType={profileType.Name}; hasEmail={hasRealEmail}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} walk-in registered {Kind} {Email} (QR {QrId})",
            actorUserId, kind, email, profile.QrId);

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
    /// Shared back-end of every create call (P7c — D-048). Routes a
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
        CancellationToken cancellationToken)
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

        // P7c — validate the ProfileTypeId before creating the user so
        // the row + the FK land atomically.
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
            if (profileType.UserType != userType)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type does not apply to this user type.",
                    "نوع الملف الشخصي المحدّد لا ينطبق على هذا النوع من المستخدمين.");
            }
        }

        var now = timeProvider.GetUtcNow();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            // Created users land in PendingApproval; the QR id is minted
            // on approval (D-046a + P4).
            AccountState = AccountState.PendingApproval,
            UserType = userType,
            // P8 — ProfileTypeId no longer lives on SimfUser; if the
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

        // P7c — RBAC roles are valid only for Admin-typed users.
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

        // P8 — if the admin picked a ProfileTypeId we drop a stub
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
            // D-167: profile-stub lands on the App DB.
            await appDbContext.SaveChangesAsync(cancellationToken);
        }

        // 7-day invite (D-042).
        var inviteLifetime = TimeSpan.FromDays(7);
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.PasswordReset,
            Code = VerificationCodeGenerator.Generate(),
            CreatedAt = now,
            ExpiresAt = now.Add(inviteLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        EnqueueInviteEmail(user.Email!, user.DisplayName, code.Code, inviteLifetime);

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

        // D-111: in-app welcome row for the new user — visible the first
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

        // D-112: fan-out AdminPendingApproval to every other Approved
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
        // D-186: Others = Visitor users carrying a partner-side
        // ProfileType (IsVisitor=false). The underlying account is the
        // same Visitor pool — only the linked ProfileType distinguishes.
        ListAccountsAsync(query, UserType.Visitor, profileScope: false, cancellationToken);

    public Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        // D-186: Visitors = Visitor users carrying an audience-side
        // ProfileType (IsVisitor=true) OR no ProfileType yet.
        ListAccountsAsync(query, UserType.Visitor, profileScope: true, cancellationToken);

    /// <summary>Shared back-end of every list call. Narrows to one
    /// <see cref="UserType"/> and (D-186) optionally further by the
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
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var top = query.Top switch { < 1 => 20, > 200 => 200, _ => query.Top };

        // Resolve the Administrator role id once for the per-row "is admin"
        // flag. Only Admin-typed users carry RBAC roles per the P7 model.
        var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);

        // D-186: cross-context scope guard — fetch the user-id set that
        // matches the requested profile scope. SimfUser lives in the
        // Identity DB and UserProfile + ProfileType in the App DB so EF
        // join is not available. The set is small under the current
        // single-event SIMF scale (<2k partner accounts); a future
        // multi-event variant should swap this for a batched contains
        // or replicate the scope flag onto SimfUser.
        var scopedUserIds = await ResolveProfileScopedUserIdsAsync(
            profileScope, cancellationToken);

        // P7c — narrow by UserType. The list is narrowed BEFORE any
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
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))
                || EF.Functions.Like(u.DisplayName, $"%{term}%"));
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
            // Natural order: newest first (matches the pre-D-044 behaviour).
            _ => users.OrderByDescending(u => u.CreatedAt),
        };

        var total = await users.CountAsync(cancellationToken);

        // Per-row role flag — projected inside the EF query (D-045 H1 N+1 fix).
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
                row.CreatedAt))
            .ToList();

        return GridPage<AdminUserSummary>.Of(summaries, total,
            new GridQuery { Skip = skip, Top = top });
    }

    private async Task<Guid?> GetAdministratorRoleIdAsync(CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(AdministratorRole);
        return role?.Id;
    }

    /// <summary>P4 — every CP role id present in the database. The set is
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

    // -- P4 + P7c — approval workflow (Admin / Other / Visitor) --------------

    public Task ApproveAdminAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.Admin, cancellationToken);

    public Task ApproveOtherAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.PartnerOther, cancellationToken);

    public Task ApproveVisitorAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, ApprovalScope.AudienceVisitor, cancellationToken);

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

    public Task<AdminBulkApprovalResponse> BulkApproveVisitorsAsync(
        Guid actorUserId, AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        BulkApproveAsync(actorUserId, request, ApprovalScope.AudienceVisitor, cancellationToken);

    public Task<AdminBulkApprovalResponse> BulkApproveOthersAsync(
        Guid actorUserId, AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        BulkApproveAsync(actorUserId, request, ApprovalScope.PartnerOther, cancellationToken);

    // D-186 — approval queue scope. Audience / Partner both back onto
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

    // D-186 — endpoints still take a legacy UserType parameter
    // (Visitor / Other) for backward-compat URL routing. Map it to
    // the IsVisitor flag the scope guards expect.
    private static bool ProfileScopeFromLegacyKind(UserType kind) =>
        kind == UserType.Visitor;

    // D-186 — fetch the set of SimfUser ids that match the requested
    // ProfileType.IsVisitor scope. Returns null when no profile-scope
    // filter is requested (Admin queue). The audience side includes
    // users with no ProfileType yet — a self-signed-up visitor with
    // no admin-assigned tier still lands on the Visitors queue.
    private async Task<HashSet<Guid>?> ResolveProfileScopedUserIdsAsync(
        bool? profileScope, CancellationToken cancellationToken)
    {
        if (profileScope is null) { return null; }
        var requireIsVisitor = profileScope.Value;

        var matchingProfileTypeIds = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.IsVisitor == requireIsVisitor
                        && p.UserType == UserType.Visitor)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var idsViaProfileType = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.ProfileTypeId != null
                && matchingProfileTypeIds.Contains(p.ProfileTypeId.Value))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        if (!requireIsVisitor)
        {
            // Partner scope = only users explicitly linked to a partner
            // ProfileType. No-profile users are NOT partners.
            return new HashSet<Guid>(idsViaProfileType);
        }

        // Audience scope = users with an audience ProfileType OR no
        // ProfileType at all. Cross-context: query the two contexts
        // separately and compute the union in memory (EF can't
        // translate a `appDbContext.UserProfiles.Any(...)` predicate
        // against a `dbContext.Users` query).
        var visitorIds = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Visitor)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var withAnyProfile = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => visitorIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);
        var withAnyProfileSet = new HashSet<Guid>(withAnyProfile);

        var set = new HashSet<Guid>(idsViaProfileType);
        foreach (var id in visitorIds)
        {
            // Include users that have no profile row yet (audience-side
            // default). Users with an audience-side profile row are
            // already in idsViaProfileType.
            if (!withAnyProfileSet.Contains(id))
            {
                set.Add(id);
            }
        }
        return set;
    }

    // D-186 — duplicate helper that inspects the source's linked
    // ProfileType to decide whether the duplicate lands on the
    // audience queue or the partner queue. Partner duplicates require
    // the source to have an explicit ProfileTypeId.
    private async Task<AdminCreateUserResponse> DuplicateVisitorScopedAsync(
        Guid actorUserId, SimfUser source, string newEmail,
        Guid? sourceProfileTypeId, CancellationToken cancellationToken)
    {
        var isPartnerSource = sourceProfileTypeId is not null
            && await appDbContext.ProfileTypes
                .AsNoTracking()
                .AnyAsync(p => p.Id == sourceProfileTypeId.Value
                            && p.IsVisitor == false,
                          cancellationToken);

        if (isPartnerSource)
        {
            return await CreateOtherAsync(actorUserId,
                new AdminCreateOtherRequest
                {
                    Email = newEmail,
                    DisplayName = source.DisplayName,
                    ProfileTypeId = sourceProfileTypeId!.Value,
                },
                cancellationToken);
        }
        return await CreateVisitorAsync(actorUserId,
            new AdminCreateVisitorRequest
            {
                Email = newEmail,
                DisplayName = source.DisplayName,
                ProfileTypeId = sourceProfileTypeId,
            },
            cancellationToken);
    }

    private async Task<AdminBulkApprovalResponse> BulkApproveAsync(
        Guid actorUserId, AdminBulkApprovalRequest request, ApprovalScope scope,
        CancellationToken cancellationToken)
    {
        // D-164 — distinct ids; cap at 500 per request so the batch fits
        // inside one reasonable transaction window without dragging the
        // hot path. The endpoint rejects empty arrays at the validator.
        var ids = request.Ids.Distinct().Take(500).ToList();
        var approved = 0;
        var failures = new List<AdminBulkApprovalFailure>();
        foreach (var subjectId in ids)
        {
            try
            {
                await ApproveAsync(actorUserId, subjectId, scope, cancellationToken);
                approved++;
            }
            catch (ApiException ex)
            {
                // Each subject's failure is bilingual + typed so the CP
                // can render the inline error list next to each row.
                var email = await accounts
                    .FindByIdAsync(subjectId, cancellationToken);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId,
                    email?.Email,
                    ex.Code,
                    ex.Message,
                    ex.MessageArabic));
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk-approve subject {SubjectId} failed for actor {ActorId}",
                    subjectId, actorUserId);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId, null, ErrorCodes.InternalError,
                    "An unexpected error prevented this approval.",
                    "حدث خطأ غير متوقع أثناء اعتماد هذا المستخدم."));
            }
        }
        return new AdminBulkApprovalResponse(approved, failures.Count, failures);
    }

    private async Task ApproveAsync(
        Guid actorUserId, Guid subjectUserId, ApprovalScope scope,
        CancellationToken cancellationToken)
    {
        var subject = await LoadPendingSubjectAsync(
            subjectUserId, scope, cancellationToken);
        var now = timeProvider.GetUtcNow();
        subject.AccountState = AccountState.Approved;
        subject.UpdatedAt = now;
        subject.StateChangedAt = now;
        subject.StateChangedByUserId = actorUserId;

        // D-106: QR + rejection text live on UserProfile now. Ensure the
        // profile row exists (it usually does — the visitor filled in
        // their form — but an admin-created Visitor / Other might be
        // approved before any profile data is captured). Clear any
        // prior rejection text (the reconsider path) and mint the QR.
        var profile = await EnsureUserProfileAsync(subject.Id, now, cancellationToken);
        profile.RejectionReason = null;
        profile.RejectionReasonArabic = null;
        await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);

        await accounts.UpdateAsync(subject).EnsureSuccessAsync();
        // D-167: UserProfile lives on App DB; save it separately from
        // the Identity-side user-state flip.
        await appDbContext.SaveChangesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // P10 — revoke every refresh token so the subject's next API
        // call gets a fresh access token with account_state=Approved.
        // Without this, a previously-pending session could keep using
        // its stale token for ≤ 15 minutes.
        await refreshTokenRepository.RevokeAllForUserAsync(
            subject.Id, now, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = ApprovalEventType(scope, approved: true),
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            Detail = profile.QrId,
        }, cancellationToken);

        // P13 — D-054: notify the approved user (with their QR id) +
        // email.
        var approvedTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = subject.DisplayName,
            ["QrId"] = profile.QrId ?? string.Empty,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = subject.Id,
            Kind = NotificationKind.AccountApproved,
            Title = "Your SIMF account is approved",
            TitleArabic = "تم اعتماد حسابك في SIMF",
            Body = $"Your event QR id is {profile.QrId}. Sign in to view it on your profile.",
            BodyArabic = $"رمز QR الخاص بك للفعالية هو {profile.QrId}. سجّل الدخول لعرضه في ملفك الشخصي.",
            Severity = NotificationSeverity.Success,
            SendEmail = true,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountApproved, "en", approvedTokens),
        }, cancellationToken);
    }

    private async Task RejectAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        ApprovalScope scope, CancellationToken cancellationToken)
    {
        var subject = await LoadPendingSubjectAsync(
            subjectUserId, scope, cancellationToken);
        var now = timeProvider.GetUtcNow();
        subject.AccountState = AccountState.Rejected;
        subject.UpdatedAt = now;
        subject.StateChangedAt = now;
        subject.StateChangedByUserId = actorUserId;

        // D-106: persist the rejection text on UserProfile (was on the
        // user row pre-D-106). EN-only admin input mirrors to the Arabic
        // column as a graceful fallback (R1 default).
        var profile = await EnsureUserProfileAsync(subject.Id, now, cancellationToken);
        profile.RejectionReason = request.Reason;
        profile.RejectionReasonArabic = request.Reason;

        await accounts.UpdateAsync(subject).EnsureSuccessAsync();
        // D-167: UserProfile lives on App DB.
        await appDbContext.SaveChangesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // P10 — revoke every refresh token so the subject's next API
        // call mints a token with account_state=Rejected (and the P11
        // authorization handler then routes them to the rejected page).
        await refreshTokenRepository.RevokeAllForUserAsync(
            subject.Id, now, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = ApprovalEventType(scope, approved: false),
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            Detail = request.Reason,
        }, cancellationToken);

        // P13 — D-054: notify the rejected user (with the reason) +
        // email.
        var rejectedTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = subject.DisplayName,
            ["Reason"] = request.Reason,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = subject.Id,
            Kind = NotificationKind.AccountRejected,
            Title = "Your SIMF account was not approved",
            TitleArabic = "لم يتم اعتماد حسابك في SIMF",
            Body = $"Reason: {request.Reason}",
            BodyArabic = $"السبب: {request.Reason}",
            Severity = NotificationSeverity.Error,
            SendEmail = true,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountRejected, "en", rejectedTokens),
        }, cancellationToken);
    }

    /// <summary>P7c — maps an approval scope + outcome to the right
    /// audit event name. D-186 reframed the discriminator from UserType
    /// (Visitor / Other / Admin) to ApprovalScope (AudienceVisitor /
    /// PartnerOther / Admin) so the audit-event names stay the same
    /// even though Other accounts are now Visitor-typed under the
    /// hood.</summary>
    private static string ApprovalEventType(ApprovalScope scope, bool approved) => scope switch
    {
        ApprovalScope.Admin when approved => AuditEvents.AdminStaffApproved,
        ApprovalScope.Admin => AuditEvents.AdminStaffRejected,
        ApprovalScope.PartnerOther when approved => AuditEvents.AdminOtherApproved,
        ApprovalScope.PartnerOther => AuditEvents.AdminOtherRejected,
        _ when approved => AuditEvents.AdminVisitorApproved,
        _ => AuditEvents.AdminVisitorRejected,
    };

    /// <summary>Loads a user that must currently be in PendingApproval
    /// **and** match the expected approval scope (D-186) — any other
    /// state or a scope-mismatch throws <see cref="ApiException"/>.
    /// Shared by every approve/reject path; the scope check closes the
    /// "approve an admin via the visitor URL" hole AND the new D-186
    /// "approve a sponsor via the visitors URL" hole.</summary>
    private async Task<SimfUser> LoadPendingSubjectAsync(
        Guid subjectUserId, ApprovalScope scope, CancellationToken cancellationToken)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        if (subject.UserType != UserTypeOf(scope))
        {
            throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account is not of the expected type.",
                "نوع الحساب المستهدف لا يطابق المتوقع.");
        }
        // D-186: within the Visitor scope, also enforce the
        // audience-vs-partner queue match via the linked ProfileType.
        var requireProfileScope = ProfileScopeOf(scope);
        if (requireProfileScope is not null
            && !await SubjectMatchesProfileScopeAsync(
                subject.Id, requireProfileScope.Value, cancellationToken))
        {
            throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account is not in the expected approval queue.",
                "الحساب المستهدف ليس في قائمة الاعتماد المتوقعة.");
        }
        if (subject.AccountState != AccountState.PendingApproval)
        {
            throw new ApiException(ErrorCodes.AdminUserNotPending, 409,
                "The target account is not pending approval.",
                "الحساب المستهدف ليس في انتظار الموافقة.");
        }
        return subject;
    }

    // D-186 — checks the subject's linked ProfileType.IsVisitor matches
    // the expected scope flag. Audience scope (true) accepts a missing
    // ProfileType (a self-signed-up visitor without an admin-assigned
    // type still lands on the audience queue); partner scope (false)
    // rejects a missing ProfileType — a user without a partner-side
    // type is not a partner.
    private async Task<bool> SubjectMatchesProfileScopeAsync(
        Guid subjectUserId, bool requireIsVisitor,
        CancellationToken cancellationToken)
    {
        var profileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == subjectUserId)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileTypeId is null)
        {
            return requireIsVisitor;
        }
        var isVisitor = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.Id == profileTypeId)
            .Select(p => (bool?)p.IsVisitor)
            .SingleOrDefaultAsync(cancellationToken);
        return (isVisitor ?? true) == requireIsVisitor;
    }

    /// <summary>
    /// D-106: returns the tracked <see cref="UserProfile"/> for the user,
    /// creating a stub row if none exists. The approve/reject flows need
    /// a profile row to write the QR / rejection text onto. Admin-typed
    /// users normally never reach approve/reject (Admins don't carry a
    /// profile today), but Visitor / Other accounts created by an admin
    /// can be approved before the user fills out the profile form —
    /// those rows get a minimal stub here so the QR has somewhere to
    /// land. The caller commits via SaveChangesAsync after the rest of
    /// the unit of work completes.
    /// </summary>
    private async Task<UserProfile> EnsureUserProfileAsync(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = await appDbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null) { return profile; }

        profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
        };
        appDbContext.UserProfiles.Add(profile);
        return profile;
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

    /// <summary>P7c — pending-approval list narrowed by UserType.
    /// D-186: <paramref name="profileScope"/> further narrows the
    /// Visitor scope into the audience (true) and partner (false)
    /// approval queues; null = no profile-scope filter (Admin queue).</summary>
    private async Task<GridPage<AdminPendingUserSummary>> ListPendingAsync(
        GridQuery query, UserType userType, bool? profileScope,
        CancellationToken cancellationToken)
    {
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var top = query.Top switch { < 1 => 20, > 200 => 200, _ => query.Top };

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

        var total = await users.CountAsync(cancellationToken);
        var page = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip).Take(top)
            .Select(u => new AdminPendingUserSummary(
                u.Id, u.Email!, u.DisplayName, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminPendingUserSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }


    public async Task<AdminBulkDeleteResponse> BulkDeleteUsersAsync(
        Guid actorUserId,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var deleted = 0;
        var skipped = 0;

        foreach (var targetId in request.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = await accounts.FindByIdAsync(targetId, cancellationToken);
            if (target is null)
            {
                // D-045 H1: unknown-id is now audited (an enumeration probe
                // against the user table is the exact signature of admin
                // abuse) — was a silent skip before.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectUserId = targetId,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.AdminUserNotFound,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }
            if (target.Id == actorUserId
                || await accounts.IsInRoleAsync(target, AdministratorRole))
            {
                // Self-delete and Administrator-vs-Administrator are blocked
                // silently per target — never explode the batch. The skipped
                // count tells the admin how many were left untouched.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = target.Id == actorUserId
                        ? ErrorCodes.AdminCannotResetSelf
                        : ErrorCodes.AdminCannotResetAdministrator,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }

            // D-045 H1: every subject wipes state + revokes sessions inside
            // one transaction; check UpdateAsync.Succeeded so a silent
            // Identity error doesn't pretend success. The audit row is
            // committed in the same transaction as the state change so SOC
            // never sees a delete-without-audit pair.
            var success = false;
            string? failureDetail = null;
            try
            {
                await transactionRunner.ExecuteAsync(async (innerCt) =>
                {
                    target.AccountState = AccountState.Disabled;
                    target.UpdatedAt = now;
                    var updateResult = await accounts.UpdateAsync(target);
                    if (!updateResult.Succeeded)
                    {
                        failureDetail = string.Join("; ",
                            updateResult.Errors.Select(error => error.Description));
                        return;
                    }
                    await accounts.UpdateSecurityStampAsync(target);
                    await refreshTokenRepository.RevokeAllForUserAsync(
                        target.Id, now, innerCt);
                    await auditLog.WriteAsync(new AuditEntry
                    {
                        EventType = AuditEvents.AdminUserDeleted,
                        Outcome = AuditOutcome.Success,
                        SubjectEmail = target.Email,
                        SubjectUserId = target.Id,
                        ActorUserId = actorUserId,
                        Detail = request.Reason,
                    }, innerCt);
                    success = true;
                }, cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                failureDetail = exception.Message;
            }

            if (success)
            {
                deleted++;
            }
            else
            {
                // Outside the rolled-back transaction so the failure row
                // survives even when the work transaction did not.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.InternalError,
                    Detail = failureDetail ?? "Delete failed without a recorded reason.",
                }, cancellationToken);
                skipped++;
            }
        }

        logger.LogInformation(
            "Admin {ActorId} bulk-deleted {Deleted} users (skipped {Skipped})",
            actorUserId, deleted, skipped);
        return new AdminBulkDeleteResponse(deleted, skipped);
    }

    public async Task<AdminCreateUserResponse> DuplicateUserAsync(
        Guid actorUserId,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await accounts.FindByIdAsync(request.SourceId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The source account was not found.",
                "لم يتم العثور على الحساب المصدر.");

        // P7c — the duplicate keeps the source's UserType + role-membership
        // shape: an Admin source duplicates as an Admin (with the same roles);
        // an Other / Visitor source duplicates as the same UserType. P8 — the
        // source's ProfileTypeId now lives on the source's UserProfile row;
        // look it up and pass it through.
        var sourceRoles = await accounts.GetRolesAsync(source);
        var sourceProfileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == source.Id)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        // D-186: source UserType is now Admin or Visitor. For Visitor
        // sources we check the linked ProfileType.IsVisitor to decide
        // whether to route through CreateOther (partner scope) or
        // CreateVisitor (audience scope) so the duplicate inherits the
        // source's queue.
        var created = source.UserType == UserType.Admin
            ? await CreateAdminAsync(actorUserId,
                new AdminCreateAdminRequest
                {
                    Email = request.NewEmail,
                    DisplayName = source.DisplayName,
                    Roles = sourceRoles.ToList(),
                },
                cancellationToken)
            : await DuplicateVisitorScopedAsync(
                actorUserId, source, request.NewEmail,
                sourceProfileTypeId, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUserDuplicated,
            Outcome = AuditOutcome.Success,
            SubjectEmail = request.NewEmail,
            SubjectUserId = created.UserId,
            ActorUserId = actorUserId,
            Detail = $"source={request.SourceId}",
        }, cancellationToken);

        return created;
    }

    public async Task<byte[]> ExportUsersAsync(
        Guid actorUserId,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AdminUserSummary> rows;
        if (request.Ids.Count > 0)
        {
            // Selected-ids path — pull, with the role flag projected in one
            // query (D-045 H1, kills the per-row IsInRoleAsync N+1).
            var idSet = request.Ids.ToHashSet();
            var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);
            var projected = await dbContext.Users
                .Where(u => idSet.Contains(u.Id))
                .Select(u => new
                {
                    u.Id, u.Email, u.DisplayName, u.AccountState,
                    u.TwoFactorEnabled, u.CreatedAt,
                    IsAdmin = adminRoleId != null
                        && dbContext.UserRoles.Any(ur =>
                            ur.UserId == u.Id && ur.RoleId == adminRoleId),
                })
                .ToListAsync(cancellationToken);
            rows = projected
                .Select(p => new AdminUserSummary(
                    p.Id, p.Email ?? string.Empty, p.DisplayName,
                    p.AccountState.ToString(), p.TwoFactorEnabled, p.IsAdmin,
                    p.CreatedAt))
                .ToList();
        }
        else
        {
            // Whole-result-set path — re-run the same query the grid used.
            // Bound to 5 000 rows so an accidental "export everything"
            // doesn't load the entire database into RAM.
            //
            // D-045 H1: clone the incoming query before mutating Skip/Top —
            // the caller may still be holding a reference to it (the CP page
            // does — UsersList.razor passes its own `_query` in), and
            // mutating it in place silently changed the page-size on the
            // next list call.
            var source = request.Query ?? new GridQuery();
            var query = new GridQuery
            {
                Skip = 0,
                Top = 5_000,
                Search = source.Search,
                Sort = source.Sort,
                SortDescending = source.SortDescending,
                Filters = new Dictionary<string, string>(source.Filters),
            };
            // P7c — export operates on the Admin family today (the
            // /admin/admins grid is the only consumer that triggers it).
            // When the Other / Visitor grids grow their own export, this
            // branches on a request-side `UserType` filter.
            var page = await ListAdminsAsync(query, cancellationToken);
            rows = page.Items;
        }

        var bytes = excel.Export(rows);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersExported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"count={rows.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} exported {Count} users to XLSX",
            actorUserId, rows.Count);
        return bytes;
    }

    public async Task<AdminImportUsersResponse> ImportUsersAsync(
        Guid actorUserId,
        byte[] xlsx,
        CancellationToken cancellationToken = default)
    {
        if (xlsx is null || xlsx.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminImportEmpty, 400,
                "An Excel file is required.",
                "ملف Excel مطلوب.");
        }

        var rows = excel.Parse(xlsx);
        var errors = new List<AdminImportError>();
        var created = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Email) || !row.Email.Contains('@'))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The email address is missing or invalid."));
                skipped++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.DisplayName))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The display name is missing."));
                skipped++;
                continue;
            }
            try
            {
                // P7c — the XLSX import is the Admin-family bulk-create
                // path. The `IsAdministrator` flag on the imported row
                // chooses whether the new admin gets the Administrator
                // RBAC role; the UserType is always Admin here.
                await CreateAdminAsync(actorUserId,
                    new AdminCreateAdminRequest
                    {
                        Email = row.Email,
                        DisplayName = row.DisplayName,
                        Roles = row.IsAdministrator
                            ? new List<string> { AppRoles.Administrator }
                            : new List<string>(),
                    },
                    cancellationToken);
                created++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminEmailAlreadyRegistered)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "An account with this email already exists."));
                skipped++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersImported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"created={created}, skipped={skipped}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} users from XLSX (skipped {Skipped})",
            actorUserId, created, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
    }

    // -- D-113 — type-scoped bulk operations for /admin/visitors/* and
    //            /admin/others/*. Each method narrows the existing helper
    //            by SimfUser.UserType so the Admin grid surface above
    //            stays bit-for-bit unchanged.

    public async Task<AdminBulkDeleteResponse> BulkDeleteUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var deleted = 0;
        var skipped = 0;

        foreach (var targetId in request.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = await accounts.FindByIdAsync(targetId, cancellationToken);
            var scopeOk = target is not null
                && target.UserType == kind
                && (requirePartnerScope is null
                    || await SubjectMatchesProfileScopeAsync(
                        target.Id, !requirePartnerScope.Value, cancellationToken));
            if (target is null || !scopeOk)
            {
                // Wrong-type or missing — audited as the same "not found"
                // code so a probing admin cannot tell the two apart.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectUserId = targetId,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.AdminUserNotFound,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }
            if (target.Id == actorUserId
                || await accounts.IsInRoleAsync(target, AdministratorRole))
            {
                // Self-delete and Administrator-vs-Administrator are blocked
                // silently per target — identical guard to the Admin-grid
                // path so a stray Administrator-roled Visitor / Other in
                // the batch still can't be deleted via this surface.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = target.Id == actorUserId
                        ? ErrorCodes.AdminCannotResetSelf
                        : ErrorCodes.AdminCannotResetAdministrator,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }

            var success = false;
            string? failureDetail = null;
            try
            {
                await transactionRunner.ExecuteAsync(async (innerCt) =>
                {
                    target.AccountState = AccountState.Disabled;
                    target.UpdatedAt = now;
                    var updateResult = await accounts.UpdateAsync(target);
                    if (!updateResult.Succeeded)
                    {
                        failureDetail = string.Join("; ",
                            updateResult.Errors.Select(error => error.Description));
                        return;
                    }
                    await accounts.UpdateSecurityStampAsync(target);
                    await refreshTokenRepository.RevokeAllForUserAsync(
                        target.Id, now, innerCt);
                    await auditLog.WriteAsync(new AuditEntry
                    {
                        EventType = AuditEvents.AdminUserDeleted,
                        Outcome = AuditOutcome.Success,
                        SubjectEmail = target.Email,
                        SubjectUserId = target.Id,
                        ActorUserId = actorUserId,
                        Detail = $"kind={kind}; {request.Reason}",
                    }, innerCt);
                    success = true;
                }, cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                failureDetail = exception.Message;
            }

            if (success)
            {
                deleted++;
            }
            else
            {
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.InternalError,
                    Detail = failureDetail ?? "Delete failed without a recorded reason.",
                }, cancellationToken);
                skipped++;
            }
        }

        logger.LogInformation(
            "Admin {ActorId} bulk-deleted {Deleted} {Kind} (skipped {Skipped})",
            actorUserId, deleted, kind, skipped);
        return new AdminBulkDeleteResponse(deleted, skipped);
    }

    public async Task<AdminCreateUserResponse> DuplicateUserByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await accounts.FindByIdAsync(request.SourceId, cancellationToken);
        var scopeOk = source is not null
            && source.UserType == kind
            && (requirePartnerScope is null
                || await SubjectMatchesProfileScopeAsync(
                    source.Id, !requirePartnerScope.Value, cancellationToken));
        if (source is null || !scopeOk)
        {
            // 404 for either branch — never reveal "the id exists but is
            // the wrong type / wrong scope" to the duplicating admin.
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The source account was not found.",
                "لم يتم العثور على الحساب المصدر.");
        }
        // Source UserType is already proven == kind, so DuplicateUserAsync's
        // switch lands on the matching CreateXxxAsync without any extra
        // guarding. Reuses the canonical implementation.
        return await DuplicateUserAsync(actorUserId, request, cancellationToken);
    }

    public async Task<byte[]> ExportUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AdminUserSummary> rows;
        // D-186: convert the new partner-scope flag to the audience-vs-
        // partner profileScope notion ListAccountsAsync uses.
        bool? profileScope = requirePartnerScope switch
        {
            true => false,    // partner side → ProfileType.IsVisitor=false
            false => true,    // audience side → ProfileType.IsVisitor=true (or no profile)
            null => null,
        };
        if (request.Ids.Count > 0)
        {
            // Selected-ids path — narrow by both id AND UserType so a
            // smuggled wrong-type id never appears in the workbook.
            // D-186: also narrow by the profile-scope id set so a
            // wrong-scope id (e.g. an audience-side visitor smuggled
            // into the Others export) does not leak in either.
            var idSet = request.Ids.ToHashSet();
            var scopedIds = await ResolveProfileScopedUserIdsAsync(
                profileScope, cancellationToken);
            if (scopedIds is not null)
            {
                idSet.IntersectWith(scopedIds);
            }
            var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);
            var projected = await dbContext.Users
                .Where(u => idSet.Contains(u.Id) && u.UserType == kind)
                .Select(u => new
                {
                    u.Id, u.Email, u.DisplayName, u.AccountState,
                    u.TwoFactorEnabled, u.CreatedAt,
                    IsAdmin = adminRoleId != null
                        && dbContext.UserRoles.Any(ur =>
                            ur.UserId == u.Id && ur.RoleId == adminRoleId),
                })
                .ToListAsync(cancellationToken);
            rows = projected
                .Select(p => new AdminUserSummary(
                    p.Id, p.Email ?? string.Empty, p.DisplayName,
                    p.AccountState.ToString(), p.TwoFactorEnabled, p.IsAdmin,
                    p.CreatedAt))
                .ToList();
        }
        else
        {
            // Whole-result-set path — bounded export of the matching kind.
            var source = request.Query ?? new GridQuery();
            var query = new GridQuery
            {
                Skip = 0,
                Top = 5_000,
                Search = source.Search,
                Sort = source.Sort,
                SortDescending = source.SortDescending,
                Filters = new Dictionary<string, string>(source.Filters),
            };
            var page = await ListAccountsAsync(query, kind, profileScope, cancellationToken);
            rows = page.Items;
        }

        var bytes = excel.Export(rows);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersExported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"kind={kind}; count={rows.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} exported {Count} {Kind} to XLSX",
            actorUserId, rows.Count, kind);
        return bytes;
    }

    // D-186 — bulk-import always creates Visitor-typed accounts;
    // partnerScope=true additionally requires a ProfileTypeId per row
    // and the chosen ProfileType.IsVisitor must be false.
    public async Task<AdminImportUsersResponse> ImportUsersByKindAsync(
        Guid actorUserId,
        bool partnerScope,
        byte[] xlsx,
        CancellationToken cancellationToken = default)
    {
        var kind = UserType.Visitor;
        if (xlsx is null || xlsx.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminImportEmpty, 400,
                "An Excel file is required.",
                "ملف Excel مطلوب.");
        }

        var rows = excel.Parse(xlsx);
        var errors = new List<AdminImportError>();
        var created = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Email) || !row.Email.Contains('@'))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The email address is missing or invalid."));
                skipped++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.DisplayName))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The display name is missing."));
                skipped++;
                continue;
            }
            // D-186: partner-side imports require a parseable
            // ProfileTypeId (matches AdminCreateOtherRequest validator).
            // Audience-side imports accept null (tier is optional).
            if (partnerScope && row.ProfileTypeId is null)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "A ProfileTypeId is required for a partner-kind import."));
                skipped++;
                continue;
            }

            try
            {
                if (partnerScope)
                {
                    // ProfileTypeId is non-null per the guard above.
                    await CreateOtherAsync(actorUserId,
                        new AdminCreateOtherRequest
                        {
                            Email = row.Email,
                            DisplayName = row.DisplayName,
                            ProfileTypeId = row.ProfileTypeId!.Value,
                        },
                        cancellationToken);
                }
                else
                {
                    await CreateVisitorAsync(actorUserId,
                        new AdminCreateVisitorRequest
                        {
                            Email = row.Email,
                            DisplayName = row.DisplayName,
                            ProfileTypeId = row.ProfileTypeId,
                        },
                        cancellationToken);
                }
                created++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminEmailAlreadyRegistered)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "An account with this email already exists."));
                skipped++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminProfileTypeInvalid)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The selected profile type is not valid for this user type."));
                skipped++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersImported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"kind={kind}; created={created}; skipped={skipped}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} {Kind} from XLSX (skipped {Skipped})",
            actorUserId, created, kind, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
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
