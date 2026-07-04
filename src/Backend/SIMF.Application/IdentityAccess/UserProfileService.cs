// Tests: SIMF.Api.Tests/UserProfileTests.cs (upsert round-trip, ID image
//        round-trip, get-empty-when-not-saved-yet, nationality-unknown,
//        D-374 Me_profileComplete flip + male-without-photo, D-609
//        DisplayName-placeholder-replaced + admin-name-preserved, D-611
//        RegionId round-trip + optional + unknown/inactive → 400)
//        SIMF.Api.Tests/UserProfileRollbackTests.cs (H16 — transaction rollback)
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// User self-service profile + encrypted ID-document storage (decisions
/// D-046 b, P8 — D-049; renamed from <c>VisitorProfileService</c>). The
/// actor identity is taken from the access token (the endpoint resolves
/// <c>sub</c>); every call operates on the actor's own row, so the
/// service does not need an admin-vs-self check.
///
/// <para>R4 — D-209: moved from <c>SIMF.Infrastructure.Identity</c>;
/// persistence is delegated to <see cref="IUserProfileRepository"/> (which
/// spans both DBs). This service keeps only the orchestration — validation,
/// the admin-wins precedence, the interest diff, the two-phase commit
/// ordering, audit, and notification dispatch.</para>
/// </summary>
internal sealed class UserProfileService(
    IUserAccountRepository accounts,
    IUserProfileRepository profiles,
    IUserIdDocumentStorage idStorage,
    IVipPhotoStorage vipPhotoStorage,
    SIMF.Application.Abstractions.IUploadScanner uploadScanner,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    ITransactionRunner transactionRunner,
    IRefreshTokenRepository refreshTokens,
    ILogger<UserProfileService> logger) : IUserProfileService
{
    public async Task<UserProfileResponse> GetMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        var profile = await profiles.GetWithInterestsAsync(actorUserId, tracked: false, cancellationToken);

        if (profile is null)
        {
            // Empty response — the user has not filled the form yet. The
            // QR id lives on the profile now (D-106), so when no profile
            // row exists yet the QR isn't available either; the page
            // will render the empty form without a QR until the user
            // saves the form.
            return new UserProfileResponse();
        }

        var nationalityCode = await profiles.ResolveCountryCodeAsync(profile.NationalityId, cancellationToken);
        return ToResponse(profile, profile.QrId, nationalityCode,
            !string.IsNullOrEmpty(user.AvatarRelativePath));
    }

    public async Task<UserProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        // D-151 — resolve the wire-side code to the Country PK. The
        // validator already checked shape; here we enforce the existence
        // rule against the live Country table (in SimfAppDbContext).
        var nationalityId = await profiles.ResolveCountryIdAsync(request.NationalityCode, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ProfileNationalityUnknown, 400,
                $"Nationality code '{request.NationalityCode}' is not supported.",
                $"الجنسية '{request.NationalityCode}' غير مدعومة.");

        var user = await accounts.FindByIdAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        // D-190 — when the user self-picked a ProfileType on the
        // sign-up screen, validate it exists, is active, AND belongs
        // to the Visitor scope (UserType=Visitor). Admin-scope rows
        // are never valid for a self-registering user. The
        // admin-wins precedence check happens below (after the
        // existing profile row is loaded).
        if (request.ProfileTypeId is { } pickedProfileTypeId)
        {
            var pickedProfileType = await profiles.FindProfileTypeAsync(pickedProfileTypeId, cancellationToken);
            if (pickedProfileType is null)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type is not valid.",
                    "نوع الملف الشخصي المحدّد غير صالح.");
            }
            if (!pickedProfileType.IsActive)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type is no longer active.",
                    "نوع الملف الشخصي المحدّد لم يعد مفعّلاً.");
            }
            if (pickedProfileType.UserType != UserType.Visitor)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type cannot be self-picked.",
                    "لا يمكن اختيار نوع الملف الشخصي هذا ذاتيًا.");
            }
            // C5 (D-371) — a self-registering visitor (audience side,
            // IsForVisitor=true) is locked to the single seeded "Normal"
            // type; richer audience tiers (VVIP/VIP/...) are admin-assigned
            // only. Partner-side ("Other") picks stay free.
            if (pickedProfileType.IsForVisitor && pickedProfileType.Name != "Normal")
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "Visitors register under the Normal profile type; other tiers are assigned by the administration.",
                    "يسجَّل الزوّار تحت النوع \"عادي\" فقط؛ الفئات الأخرى تُسند من الإدارة.");
            }
        }

        // B3 — D-221: validate the الجهة pick exists and is active. Cross-
        // context existence check (Organisation lives on the App DB), exactly
        // like the nationality / profile-type checks above.
        if (request.OrganisationId is { } organisationId
            && !await profiles.OrganisationExistsActiveAsync(organisationId, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.OrganisationInvalid, 400,
                "The selected organisation is not valid.",
                "الجهة المحددة غير صالحة.");
        }

        // D-611 (Wave B): validate the المنطقة pick exists and is active, exactly
        // like the الجهة check above. The App-DB Region table backs the pick.
        if (request.RegionId is { } regionId
            && !await profiles.RegionExistsActiveAsync(regionId, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.RegionInvalid, 400,
                "The selected region is not valid.",
                "المنطقة المحددة غير صالحة.");
        }

        // P9 — validate the picked interest ids: every id must exist
        // and be active. (The validator already enforces 1-10 count.)
        var requestedIds = request.InterestIds.Distinct().ToList();
        var foundActiveIds = await profiles.FilterActiveInterestIdsAsync(requestedIds, cancellationToken);
        if (foundActiveIds.Count != requestedIds.Count)
        {
            throw new ApiException(
                ErrorCodes.InterestInvalid, 400,
                "One or more selected interests are unknown or no longer active.",
                "بعض الاهتمامات المختارة غير معروفة أو لم تعد مفعّلة.");
        }

        var now = timeProvider.GetUtcNow();
        var profile = await profiles.GetWithInterestsAsync(actorUserId, tracked: true, cancellationToken);

        var isNew = profile is null;
        // P8 — the admin may have created a stub row with a ProfileTypeId
        // already set (e.g. via /admin/others). Preserve it.
        profile ??= new UserProfile { UserId = actorUserId, CreatedAt = now };

        // Two-photo split (D-431-follow-up) — the profile carries two distinct
        // images, each uploaded BEFORE this save:
        //   • The FACE photo (SimfUser.AvatarRelativePath, live capture) is HARD-
        //     required for MALE registrants here — the direct successor of the
        //     D-431 male-photo gate that closed the save-then-bounce login loop
        //     (the loop the owner reported was the male photo). Avatar upload
        //     does NOT seed a profile stub, so this gate does not interfere with
        //     the first-submit account-state transition below.
        //   • The ID DOCUMENT (IdImageRelativePath, gallery upload) is mandatory
        //     for EVERY registrant but is enforced by the client form + the
        //     server completeness flag (IsProfileCompleteAsync below), NOT a hard
        //     reject here: the ID upload seeds the stub row, so hard-gating it
        //     would force the upload ordering and collide with the "no profile
        //     row" rollback guarantee (H16). The admin walk-in desk
        //     (AdminAccountService.RegisterOnSiteAsync) is a separate capture
        //     path and is intentionally not gated here.
        if (request.Gender == Gender.Male
            && string.IsNullOrEmpty(user.AvatarRelativePath))
        {
            throw new ApiException(
                ErrorCodes.VisitorFaceImageMissing, 400,
                "A face photo is required before a male registrant's profile can be saved. Capture the face photo, then try again.",
                "يلزم التقاط صورة شخصية للوجه قبل حفظ ملف المسجِّل الذكر. التقط الصورة الشخصية ثم حاول مرة أخرى.");
        }

        // D-373 — issue the human-friendly registration reference once
        // (SIMF-<year>-<8-digit sequence>); covers brand-new rows and any
        // pre-D-373 / admin-stub rows that never received one.
        if (string.IsNullOrEmpty(profile.ReferenceNumber))
        {
            var sequenceValue = await profiles.NextRegistrationReferenceAsync(cancellationToken);
            profile.ReferenceNumber = $"SIMF-{now.Year}-{sequenceValue:D8}";
        }

        // D-190 — admin-wins precedence for ProfileTypeId.
        //   • Admin pre-assigned (existing profile.ProfileTypeId != null):
        //       keep the admin's pick; the user's self-pick is silently
        //       ignored on this surface. The admin override path lives
        //       on the admin endpoints.
        //   • No admin pick yet AND user supplied a ProfileTypeId:
        //       write the user's self-pick onto the row. This is the
        //       mobile sign-up Screen 2 path.
        //   • Request omits ProfileTypeId AND no admin pick yet:
        //       leave null; admin assigns later via the admin pending-
        //       approval review flow.
        if (profile.ProfileTypeId is null && request.ProfileTypeId is { } userPick)
        {
            profile.ProfileTypeId = userPick;
        }

        profile.NameArabic = request.ArabicName;
        profile.Name = request.EnglishName;
        profile.JobTitle = NormaliseOptional(request.JobTitle);
        profile.NationalityId = nationalityId;
        profile.DateOfBirth = request.DateOfBirth;
        profile.PlaceOfBirth = request.PlaceOfBirth;
        profile.IsSaudi = request.IsSaudi;
        profile.NationalId = request.IsSaudi ? request.NationalId : null;
        profile.IqamaNumber = request.IsSaudi ? null : request.IqamaNumber;
        profile.PassportNumber = request.IsSaudi ? null : request.PassportNumber;
        profile.SaudiMobile = NormaliseOptional(request.SaudiMobile);
        profile.InternationalMobile = NormaliseOptional(request.InternationalMobile);
        // C6 — D-371: رقم اللوحة, stored normalized (validator-checked shape;
        // separators stripped so the column holds the canonical ≤7 chars).
        profile.PlateNumber = NormalisePlate(request.PlateNumber);
        // B3 — D-221: الجهة + الجنس. D-611: المنطقة.
        profile.OrganisationId = request.OrganisationId;
        profile.RegionId = request.RegionId;
        profile.Gender = request.Gender;
        if (!isNew)
        {
            profile.UpdatedAt = now;
        }

        if (isNew)
        {
            profiles.Add(profile);
        }

        // P9 — diff the interests: remove ones no longer picked, add the
        // new ones. (Clear-then-re-add would generate DELETE + INSERT
        // for unchanged rows.)
        var requestedSet = requestedIds.ToHashSet();
        var existingIds = profile.Interests.Select(interest => interest.Id).ToHashSet();
        var toRemove = profile.Interests
            .Where(interest => !requestedSet.Contains(interest.Id))
            .ToList();
        foreach (var stale in toRemove)
        {
            profile.Interests.Remove(stale);
        }
        var toAddIds = requestedSet.Except(existingIds).ToList();
        if (toAddIds.Count > 0)
        {
            var freshRows = await profiles.GetInterestsByIdsAsync(toAddIds, cancellationToken);
            foreach (var row in freshRows)
            {
                profile.Interests.Add(row);
            }
        }

        // H2 — D-057: the profile save, the EmailVerified → PendingApproval
        // auto-transition (P13 — D-054), and the revoke of every live
        // refresh token for the user must all commit together. Without
        // the transaction, a crash between the profile save and the state
        // flip would leave the user stuck in EmailVerified (the UI never
        // re-asks for the profile), and a stale refresh token would keep
        // minting access tokens carrying account_state=EmailVerified —
        // skipping the Pending banner P11 added until the token's natural
        // expiry. Notifications stay outside the transaction (in-app
        // rows + email enqueue are not under this DB scope), so they
        // dispatch only after the commit succeeds.
        // A9c follow-up (D-609) — decide whether to replace the email-placeholder
        // DisplayName with the registrant's real name at profile completion. This
        // is the RegistrationService "DisplayName = Email … replaced at profile
        // completion" TODO: both names are validator-required (English preferred,
        // Arabic fallback). Only the untouched placeholder (DisplayName still ==
        // Email) is overwritten, so an admin-customised name is preserved. The
        // write itself happens inside the Identity transaction below.
        var realName = !string.IsNullOrWhiteSpace(request.EnglishName)
            ? request.EnglishName.Trim()
            : request.ArabicName?.Trim() ?? string.Empty;
        var renameDisplayName =
            !string.IsNullOrWhiteSpace(realName)
            && string.Equals(user.DisplayName, user.Email, StringComparison.OrdinalIgnoreCase);

        var transitioned = false;
        await transactionRunner.ExecuteAsync(async token =>
        {
            // D-167: the TransactionRunner only wraps the Identity DB
            // transaction; the App DB save is a separate physical
            // commit. Order: Identity first (state flip + token revoke);
            // App second (profile row + interests). If Identity throws,
            // the App save never runs and the row is dropped — matches
            // the historical "all or nothing" guarantee. If App throws
            // after Identity commits, the user retries the save and
            // we converge.
            await profiles.SaveIdentityChangesAsync(token);

            // The first profile submission advances an EmailVerified account to
            // PendingApproval. This is keyed on the account state, NOT on
            // `isNew`: with the two-photo split the client uploads the ID (and,
            // for men, the face) BEFORE this save, and each upload seeds the
            // profile stub row — so by the time the upsert runs the row already
            // exists (isNew == false). Keying on EmailVerified makes the flip
            // fire exactly once (the first submit after email verification),
            // regardless of whether a photo upload pre-created the row, and
            // never re-fires once the account has left EmailVerified.
            var accountDirty = false;

            // A9c follow-up (D-609) — replace the email-placeholder DisplayName
            // with the real name (set here so it commits with the profile via the
            // same accounts.UpdateAsync used for the state flip; no cross-DB txn).
            if (renameDisplayName)
            {
                user.DisplayName = realName;
                accountDirty = true;
            }

            if (user.AccountState == AccountState.EmailVerified)
            {
                user.AccountState = AccountState.PendingApproval;
                user.StateChangedAt = now;
                user.StateChangedByUserId = null;
                accountDirty = true;
                transitioned = true;
            }

            if (accountDirty)
            {
                var updateResult = await accounts.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Account update during profile save failed: " +
                        string.Join("; ", updateResult.Errors.Select(error => error.Description)));
                }
            }

            if (transitioned)
            {
                // Stale tokens still encode the old account_state claim;
                // revoke them so the user has to sign in again and the
                // next JWT reflects PendingApproval.
                await refreshTokens.RevokeAllForUserAsync(actorUserId, now, token);
            }
        }, cancellationToken);

        // D-167: App-DB commit happens AFTER the Identity transaction
        // succeeds, so an Identity-side rollback drops the profile
        // changes too (the test in UserProfileRollbackTests asserts
        // this). The window where Identity commits and App fails is
        // covered by user retry — the next upsert reattempts the App
        // save against an idempotent (UserId-unique) row.
        await profiles.SaveAppChangesAsync(cancellationToken);

        // D-190 — the audit Detail now carries the ProfileTypeId so
        // the CP pending-profile review surface shows the user's
        // self-pick (or "none" when the user submitted without
        // picking and the admin has not yet assigned one).
        var operation = isNew ? "created" : "updated";
        var profileTypeIdForAudit = profile.ProfileTypeId?.ToString() ?? "none";
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileSaved,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = $"{operation}; profileTypeId={profileTypeIdForAudit}",
        }, cancellationToken);

        logger.LogInformation(
            "User profile {Operation} for {UserId}",
            isNew ? "created" : "updated", actorUserId);

        if (transitioned)
        {
            await DispatchProfileSubmittedAsync(user, cancellationToken);
            await DispatchAdminPendingVisitorAsync(user, cancellationToken);
        }

        return ToResponse(profile, profile.QrId, request.NationalityCode.ToUpperInvariant(),
            !string.IsNullOrEmpty(user.AvatarRelativePath));
    }

    /// <summary>
    /// D-106: implements <see cref="IUserProfileService.GetRejectionTextAsync"/>.
    /// Reads the bilingual rejection text directly from UserProfile; the
    /// SignInService uses this for the AccountStateInfo state-banner.
    /// </summary>
    public Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        profiles.GetRejectionTextAsync(userId, cancellationToken);

    /// <summary>D-161 — implements <see cref="IUserProfileService.ResolveMobileAppRoleAsync"/>.
    /// Admin short-circuits to <see cref="MobileAppRole.None"/>. D-186
    /// folded Other accounts into Visitor: audience-side Visitors
    /// (ProfileType.IsVisitor=true or no ProfileType) resolve to
    /// <see cref="MobileAppRole.Visitor"/>; partner-side Visitors
    /// (ProfileType.IsVisitor=false) inherit the assigned profile-type's
    /// MobileAppRole — Staff / Moderator authority still flows from
    /// the partner ProfileType row, just under the unified UserType.</summary>
    public async Task<MobileAppRole> ResolveMobileAppRoleAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null) { return MobileAppRole.None; }
        if (user.UserType == UserType.Admin)
        {
            return MobileAppRole.None;
        }

        // D-194 — a partner ProfileType only confers Staff / Moderator
        // authority once an admin has APPROVED the account. A self-
        // registering user who self-picks a partner profile type stays
        // PendingApproval (see UpsertMineAsync), so they resolve to the
        // default Visitor role until an admin reviews and approves them
        // (the admin sees the proposed ProfileType at approval time and
        // can change it). This closes the self-service escalation: the
        // mobile sign-up API alone can never mint more than Visitor.
        // AccountState.Approved is reached only via an admin action
        // (approve / admin-create / on-site register) or the seeder —
        // never a self-service path.
        if (user.AccountState != AccountState.Approved)
        {
            return MobileAppRole.Visitor;
        }

        // Visitor scope — partner profile types carry an operational
        // MobileAppRole; audience profile types (or no profile yet)
        // resolve to the default Visitor mobile role.
        var profileType = await profiles.GetAssignedProfileTypeRoleAsync(userId, cancellationToken);
        if (profileType is null || profileType.IsVisitor)
        {
            return MobileAppRole.Visitor;
        }
        return profileType.MobileAppRole;
    }

    public async Task<bool> IsProfileCompleteAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // D-374 + the two-photo split (D-431-follow-up) — the server-side
        // completeness rule: both names + at least one interest (the validator
        // demands 1–10 on every save) + the ID document (all registrants) + the
        // face photo (men only). The ID-document path lives on the App profile
        // (one projected row); the face photo is the avatar on the Identity user
        // (D-157 cross-DB), read only when the registrant is male (women are
        // exempt, so most reads still touch one DB). Runs on every /users/me
        // hydration (sign-in + app boot).
        var facts = await profiles.GetCompletenessFactsAsync(userId, cancellationToken);
        if (facts is null)
        {
            return false;
        }
        var hasNames = !string.IsNullOrWhiteSpace(facts.NameArabic)
            && !string.IsNullOrWhiteSpace(facts.Name);
        var hasIdImage = !string.IsNullOrEmpty(facts.IdImageRelativePath);
        var maleFaceSatisfied = facts.Gender != Gender.Male;
        if (!maleFaceSatisfied)
        {
            var user = await accounts.FindByIdAsync(userId, cancellationToken);
            maleFaceSatisfied = user is not null
                && !string.IsNullOrEmpty(user.AvatarRelativePath);
        }
        return hasNames && facts.HasInterests && hasIdImage && maleFaceSatisfied;
    }

    private async Task DispatchProfileSubmittedAsync(
        SimfUser user, CancellationToken cancellationToken)
    {
        var tokens = new Dictionary<string, string>
        {
            ["DisplayName"] = user.DisplayName,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.AccountProfileSubmitted,
            Title = "Profile submitted — pending approval",
            TitleArabic = "تم إرسال الملف الشخصي — بانتظار الموافقة",
            Body = "Thank you for completing your SIMF profile. An administrator will review your account shortly.",
            BodyArabic = "شكراً لاستكمال ملفك الشخصي في SIMF. سيقوم المسؤول بمراجعة حسابك قريباً.",
            Severity = NotificationSeverity.Info,
            SendEmail = true,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountProfileSubmitted, "en", tokens),
        }, cancellationToken);
    }

    private async Task DispatchAdminPendingVisitorAsync(
        SimfUser subject, CancellationToken cancellationToken)
    {
        // Every Admin gets one in-app notification + email per pending
        // visitor. No bulk-send today; the admin count is small (event
        // ops staff).
        var admins = await profiles.ListApprovedAdminsAsync(cancellationToken);

        foreach (var admin in admins)
        {
            var tokens = new Dictionary<string, string>
            {
                ["DisplayName"] = admin.DisplayName ?? string.Empty,
                ["SubjectEmail"] = subject.Email ?? string.Empty,
            };
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = admin.Id,
                Kind = NotificationKind.AdminPendingVisitor,
                Title = $"New visitor awaiting approval — {subject.Email}",
                TitleArabic = $"زائر جديد بانتظار الموافقة — {subject.Email}",
                Body = $"A new visitor has submitted their profile: {subject.Email}.",
                BodyArabic = $"قام زائر جديد بإرسال ملفه الشخصي: {subject.Email}.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = "User",
                RelatedEntityId = subject.Id,
                SendEmail = true,
                PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                    NotificationKind.AdminPendingVisitor, "en", tokens),
            }, cancellationToken);
        }
    }

    public async Task UploadIdImageAsync(
        Guid actorUserId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        // A6-18 (NCA) — malware-scan the untrusted image before it is stored.
        await uploadScanner.EnsureCleanAsync(content, "id-document", cancellationToken);

        // ID image follows the avatar contract (D-039): magic-byte and
        // size already checked at the endpoint, the storage layer
        // encrypts and writes.
        var profile = await profiles.FindAsync(actorUserId, cancellationToken);
        if (profile is null)
        {
            // ID image only makes sense alongside a profile row — create
            // a stub so the relative path has somewhere to live.
            profile = new UserProfile
            {
                UserId = actorUserId,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            profiles.Add(profile);
        }

        var relativePath = await idStorage.SaveAsync(
            actorUserId, content, contentType, cancellationToken);
        profile.IdImageRelativePath = relativePath;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        // D-167: UserProfile is on the App DB now.
        await profiles.SaveAppChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = $"{content.Length} bytes, {contentType}",
        }, cancellationToken);
    }

    public async Task<UserIdDocumentImage?> ReadIdImageAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var path = await profiles.GetIdImagePathAsync(actorUserId, cancellationToken);
        if (path is null)
        {
            return null;
        }
        var read = await idStorage.OpenReadAsync(path, cancellationToken);
        return read is null ? null : new UserIdDocumentImage(read.Content, read.ContentType);
    }

    public async Task UploadIdImageForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        UserType expectedKind,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        if (subject.UserType != expectedKind)
        {
            // Same 404-on-mismatch policy as D-124 — no cross-kind enumeration.
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        }

        var profile = await profiles.FindAsync(subjectUserId, cancellationToken);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = subjectUserId,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            profiles.Add(profile);
        }

        // A6-18 (NCA) — malware-scan the untrusted image before it is stored.
        await uploadScanner.EnsureCleanAsync(content, "id-document", cancellationToken);

        var relativePath = await idStorage.SaveAsync(
            subjectUserId, content, contentType, cancellationToken);
        profile.IdImageRelativePath = relativePath;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        // D-167: UserProfile is on the App DB now.
        await profiles.SaveAppChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-upload; {content.Length} bytes; {contentType}",
        }, cancellationToken);
    }

    public async Task<UserIdDocumentImage?> ReadIdImageForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        UserType expectedKind,
        CancellationToken cancellationToken = default)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken);
        if (subject is null || subject.UserType != expectedKind) { return null; }

        var path = await profiles.GetIdImagePathAsync(subjectUserId, cancellationToken);
        if (path is null)
        {
            return null;
        }
        var read = await idStorage.OpenReadAsync(path, cancellationToken);
        if (read is null) { return null; }

        // A9 (PII) — an admin READ of a visitor's national-ID image is a PII
        // disclosure and must leave an audit trail, mirroring the upload's audit
        // (the write path was audited; the read was not). Only the actual byte
        // disclosure is audited — a 404 for a subject with no image on file is not.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageViewed,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-read; {read.Content.Length} bytes; {read.ContentType}",
        }, cancellationToken);

        return new UserIdDocumentImage(read.Content, read.ContentType);
    }

    public async Task UploadVipPhotoForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        UserType expectedKind,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        if (subject.UserType != expectedKind)
        {
            // Same 404-on-mismatch policy as the ID-image path — no cross-kind enumeration.
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        }

        var profile = await profiles.FindAsync(subjectUserId, cancellationToken);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = subjectUserId,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            profiles.Add(profile);
        }

        // A6-18 (NCA) — malware-scan the untrusted image before it is stored.
        await uploadScanner.EnsureCleanAsync(content, "vip-photo", cancellationToken);

        var relativePath = await vipPhotoStorage.SaveAsync(
            subjectUserId, content, contentType, cancellationToken);
        profile.VipPhotoRelativePath = relativePath;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        // D-167: UserProfile is on the App DB now.
        await profiles.SaveAppChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileVipPhotoUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-upload vip-photo; {content.Length} bytes; {contentType}",
        }, cancellationToken);
    }

    public async Task<VipPhotoImage?> ReadVipPhotoForSubjectAsync(
        Guid subjectUserId,
        UserType expectedKind,
        CancellationToken cancellationToken = default)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken);
        if (subject is null || subject.UserType != expectedKind) { return null; }

        // One-column projection (no tracking) — mirrors GetIdImagePathAsync; the
        // per-image read path doesn't need the whole tracked profile.
        var path = await profiles.GetVipPhotoPathAsync(subjectUserId, cancellationToken);
        if (path is null)
        {
            return null;
        }
        var read = await vipPhotoStorage.OpenReadAsync(path, cancellationToken);
        if (read is null) { return null; }
        using var stream = read.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return new VipPhotoImage(buffer.ToArray(), read.ContentType);
    }

    private static UserProfileResponse ToResponse(
        UserProfile profile, string? qrId, string nationalityCode, bool hasAvatar) =>
        new()
        {
            ProfileTypeId = profile.ProfileTypeId,
            InterestIds = profile.Interests.Select(interest => interest.Id).ToList(),
            ArabicName = profile.NameArabic,
            EnglishName = profile.Name,
            JobTitle = profile.JobTitle,
            NationalityCode = nationalityCode,
            DateOfBirth = profile.DateOfBirth,
            PlaceOfBirth = profile.PlaceOfBirth,
            IsSaudi = profile.IsSaudi,
            NationalId = profile.NationalId,
            IqamaNumber = profile.IqamaNumber,
            PassportNumber = profile.PassportNumber,
            SaudiMobile = profile.SaudiMobile,
            InternationalMobile = profile.InternationalMobile,
            PlateNumber = profile.PlateNumber,
            // C6 — D-459: surface both renderings of the stored canonical code.
            PlateNumberAr = SaudiPlate.ToArabic(profile.PlateNumber),
            PlateNumberEn = SaudiPlate.ToEnglish(profile.PlateNumber),
            ReferenceNumber = profile.ReferenceNumber,
            OrganisationId = profile.OrganisationId,
            RegionId = profile.RegionId,
            Gender = profile.Gender,
            HasIdImage = !string.IsNullOrEmpty(profile.IdImageRelativePath),
            HasAvatar = hasAvatar,
            QrId = qrId,
        };

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>C6 — D-459: the plate is stored as the canonical Latin "code"
    /// (Latin letters + Western digits, separators stripped) via the shared
    /// <see cref="SaudiPlate"/>. The Arabic and English renderings are derived
    /// on read (no duplicated persistence) — see <see cref="ToResponse"/>.</summary>
    private static string? NormalisePlate(string? value) => SaudiPlate.Normalize(value);
}
