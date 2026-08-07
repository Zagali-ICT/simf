// Tests: SIMF.Api.Tests/UserProfileTests.cs (upsert round-trip, ID image
//        round-trip, get-empty-when-not-saved-yet, nationality-unknown,
//        Me_profileComplete flip + male-without-photo,
//        DisplayName-placeholder-replaced + admin-name-preserved,
//        RegionId round-trip + optional + unknown/inactive → 400,
//        DEF-PHN-003 mobile stored canonicalised [Saudi theory + international],
//        DEF-PHN-004 mobile required / cannot be blanked / international-only OK)
//        SIMF.Api.Tests/UserProfileRollbackTests.cs (H16 — transaction rollback)
//        SIMF.Api.Tests/GateOperatorModelTests.cs (BUG-018 — an operational
//        (IsForVisitor=false) profile type is exempt from the visitor
//        completeness + male-face rules; the audience side is unchanged)
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
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
/// User self-service profile + encrypted ID-document storage. The actor
/// identity is taken from the access token (the endpoint resolves
/// <c>sub</c>); every call operates on the actor's own row, so the
/// service does not need an admin-vs-self check.
///
/// <para>Persistence is delegated to <see cref="IUserProfileRepository"/>
/// (which spans both DBs). This service keeps only the orchestration — validation,
/// the admin-wins precedence, the interest diff, the two-phase commit
/// ordering, audit, and notification dispatch.</para>
/// </summary>
internal sealed class UserProfileService(
    IUserAccountRepository accounts,
    IUserProfileRepository profiles,
    IPiiEncryptor pii,
    IFileService fileService,
    IFileStorageProvider fileStorage,
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
            // QR id lives on the profile, so when no profile
            // row exists yet the QR isn't available either; the page
            // will render the empty form without a QR until the user
            // saves the form.
            return new UserProfileResponse();
        }

        var nationalityCode = await profiles.ResolveCountryCodeAsync(profile.NationalityId, cancellationToken);
        var (isVip, isForVisitor) = await ResolveProfileTypeFlagsAsync(profile.ProfileTypeId, cancellationToken);
        return ToResponse(profile, profile.QrId, nationalityCode,
            !string.IsNullOrEmpty(user.AvatarRelativePath), isVip, isForVisitor);
    }

    public async Task<UserProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        // Resolve the wire-side code to the Country PK. The
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

        // When the user self-picked a ProfileType on the
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
            // The sign-up picker
            // (GET /app/account/profile-types) only offers rows where
            // IsAppRegisterable=true; the self-service write path MUST mirror
            // that server-side. Otherwise a direct POST could self-assign a
            // CP-only operational type (Staff / Moderator, IsAppRegisterable=false)
            // — which, once an admin approves the account off the "Others" queue,
            // mints that partner ProfileType's MobileAppRole. The picker filter is
            // read-side only; without this guard it is bypassed by a crafted call.
            // Fail closed: reject any self-picked non-registerable type.
            if (!pickedProfileType.IsAppRegisterable)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type cannot be self-picked.",
                    "لا يمكن اختيار نوع الملف الشخصي هذا ذاتيًا.");
            }
            if (pickedProfileType.UserType != UserType.Visitor)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type cannot be self-picked.",
                    "لا يمكن اختيار نوع الملف الشخصي هذا ذاتيًا.");
            }
            // A self-registering visitor (audience side,
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

        // Validate the الجهة pick exists and is active. Cross-
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

        // Validate the المنطقة pick exists and is active, exactly
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

        var now = timeProvider.SimfNow();
        var profile = await profiles.GetWithInterestsAsync(actorUserId, tracked: true, cancellationToken);

        var isNew = profile is null;
        // P8 — the admin may have created a stub row with a ProfileTypeId
        // already set (e.g. via /admin/others). Preserve it.
        profile ??= new UserProfile { UserId = actorUserId, CreatedAt = now };

        // Two-photo split — the profile carries two distinct
        // images, each uploaded BEFORE this save:
        //   • The FACE photo (SimfUser.AvatarRelativePath, live capture) is HARD-
        //     required for MALE registrants here — the direct successor of the
        //     male-photo gate that closed the save-then-bounce login loop
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
        // BUG-018 (18-3) — the face-photo gate is a VISITOR registration rule. The
        // effective profile type is the admin's pick when there is one (admin-wins,
        // applied below), otherwise the user's. An operational partner-side type
        // (IsForVisitor=false — a gate operator, a moderator) is exempt: a male gate
        // operator could not submit the form at all, so an admin-created operator
        // could never finish the profile their sign-in is diverted to.
        var effectiveProfileTypeId = profile.ProfileTypeId ?? request.ProfileTypeId;
        var (_, isAudienceRegistrant) =
            await ResolveProfileTypeFlagsAsync(effectiveProfileTypeId, cancellationToken);

        if (isAudienceRegistrant
            && request.Gender == Gender.Male
            && string.IsNullOrEmpty(user.AvatarRelativePath))
        {
            throw new ApiException(
                ErrorCodes.VisitorFaceImageMissing, 400,
                "A face photo is required before a male registrant's profile can be saved. Capture the face photo, then try again.",
                "يلزم التقاط صورة شخصية للوجه قبل حفظ ملف المسجِّل الذكر. التقط الصورة الشخصية ثم حاول مرة أخرى.");
        }

        // Issue the human-friendly registration reference once
        // (SIMF-<year>-<8-digit sequence>); covers brand-new rows and any
        // older / admin-stub rows that never received one.
        if (string.IsNullOrEmpty(profile.ReferenceNumber))
        {
            var sequenceValue = await profiles.NextRegistrationReferenceAsync(cancellationToken);
            profile.ReferenceNumber = $"SIMF-{now.Year}-{sequenceValue:D8}";
        }

        // Admin-wins precedence for ProfileTypeId.
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
        profile.JobTitleArabic = NormaliseOptional(request.JobTitleArabic);
        profile.NationalityId = nationalityId;
        profile.DateOfBirth = request.DateOfBirth;
        profile.PlaceOfBirth = request.PlaceOfBirth;
        profile.IsSaudi = request.IsSaudi;
        // H-1 — normalise + blind-index the identity columns exactly like the
        // walk-in desk (AdminAccountService), so the self-service write path (the
        // dominant one) also populates the hashes the filtered UNIQUE indexes and
        // the duplicate-identity guard key off. Without the hashes these rows were
        // invisible to both, defeating H-1 for the dominant registration path.
        var nationalId = request.IsSaudi ? NormaliseOptional(request.NationalId) : null;
        var iqamaNumber = request.IsSaudi ? null : NormaliseOptional(request.IqamaNumber);
        var passportNumber = request.IsSaudi ? null : NormaliseOptional(request.PassportNumber);
        profile.NationalId = nationalId;
        profile.NationalIdHash = pii.BlindIndex(nationalId);
        profile.IqamaNumber = iqamaNumber;
        profile.IqamaNumberHash = pii.BlindIndex(iqamaNumber);
        profile.PassportNumber = passportNumber;
        profile.PassportNumberHash = pii.BlindIndex(passportNumber);

        // H-1 — reject an identifier already registered on ANOTHER user's profile
        // (409). Self-excluding (UserId != actorUserId) so a user re-saving their
        // OWN id is never a false conflict. A concurrent duplicate that slips this
        // soft guard hits the filtered UNIQUE index and is translated below (FIX E).
        if (await profiles.AnyOtherProfileWithIdentityHashAsync(
                actorUserId, profile.NationalIdHash, profile.IqamaNumberHash,
                profile.PassportNumberHash, cancellationToken))
        {
            throw ApiException.DuplicateIdentity();
        }

        // DEF-PHN-003 — store the CANONICAL number (separators stripped, a
        // leading `00` rewritten to `+`), not the raw text. A plain trim let the
        // one column hold "+966501234567" from the app and "+966-555987654" from
        // the Control-Panel / Website phone input — two spellings of one number.
        // Same reasoning (and the same shared-normaliser shape) as the plate below.
        profile.SaudiMobile = MobileNumber.NormalizeOptional(request.SaudiMobile);
        profile.InternationalMobile =
            MobileNumber.NormalizeOptional(request.InternationalMobile);
        // رقم اللوحة, stored normalized (validator-checked shape;
        // separators stripped so the column holds the canonical ≤7 chars).
        profile.PlateNumber = NormalisePlate(request.PlateNumber);
        // الجهة + الجنس + المنطقة.
        profile.OrganisationId = request.OrganisationId;
        profile.RegionId = request.RegionId;
        profile.Gender = request.Gender;
        // "Show in Meet People Like You" toggle; null = no change.
        if (request.ShowInMeetLikeYou.HasValue)
        {
            profile.ShowInMeetLikeYou = request.ShowInMeetLikeYou.Value;
        }
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

        // The profile save, the EmailVerified → PendingApproval
        // auto-transition, and the revoke of every live
        // refresh token for the user must all commit together. Without
        // the transaction, a crash between the profile save and the state
        // flip would leave the user stuck in EmailVerified (the UI never
        // re-asks for the profile), and a stale refresh token would keep
        // minting access tokens carrying account_state=EmailVerified —
        // skipping the Pending banner P11 added until the token's natural
        // expiry. Notifications stay outside the transaction (in-app
        // rows + email enqueue are not under this DB scope), so they
        // dispatch only after the commit succeeds.
        // Decide whether to replace the email-placeholder
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
            // The TransactionRunner only wraps the Identity DB
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

            // Replace the email-placeholder DisplayName
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

        // App-DB commit happens AFTER the Identity transaction
        // succeeds, so an Identity-side rollback drops the profile
        // changes too (the test in UserProfileRollbackTests asserts
        // this). The window where Identity commits and App fails is
        // covered by user retry — the next upsert reattempts the App
        // save against an idempotent (UserId-unique) row.
        // FIX E — a concurrent duplicate identity that slipped the soft guard
        // above hits the filtered UNIQUE index here; the repository translates
        // that race into a 409 DuplicateIdentity instead of an uncaught 500.
        await profiles.SaveProfileIdentityChangesAsync(cancellationToken);

        // The audit Detail carries the ProfileTypeId so
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

        var (isVip, isForVisitor) = await ResolveProfileTypeFlagsAsync(profile.ProfileTypeId, cancellationToken);
        return ToResponse(profile, profile.QrId, request.NationalityCode.ToUpperInvariant(),
            !string.IsNullOrEmpty(user.AvatarRelativePath), isVip, isForVisitor);
    }

    /// <summary>
    /// Implements <see cref="IUserProfileService.GetRejectionTextAsync"/>.
    /// Reads the bilingual rejection text directly from UserProfile; the
    /// SignInService uses this for the AccountStateInfo state-banner.
    /// </summary>
    public Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        profiles.GetRejectionTextAsync(userId, cancellationToken);

    /// <summary>Implements <see cref="IUserProfileService.ResolveMobileAppRoleAsync"/>.
    /// Admin short-circuits to <see cref="MobileAppRole.None"/>. Other accounts
    /// were folded into Visitor: audience-side Visitors
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

        // A partner ProfileType only confers Staff / Moderator
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
        // The two-photo split — the server-side
        // completeness rule: both names + at least one interest (the validator
        // demands 1–10 on every save) + the ID document (all registrants) + the
        // face photo (men only). The ID-document path lives on the App profile
        // (one projected row); the face photo is the avatar on the Identity user
        // (cross-DB), read only when the registrant is male (women are
        // exempt, so most reads still touch one DB). Runs on every /users/me
        // hydration (sign-in + app boot).
        var facts = await profiles.GetCompletenessFactsAsync(userId, cancellationToken);
        if (facts is null)
        {
            return false;
        }
        var hasNames = !string.IsNullOrWhiteSpace(facts.NameArabic)
            && !string.IsNullOrWhiteSpace(facts.Name);

        // BUG-018 (18-3) — the interest / ID-document / male-face evidence is a
        // VISITOR registration requirement. An operational partner-side account
        // (ProfileType.IsForVisitor=false — a gate operator, a moderator) is created
        // and vetted by an admin, so holding it to the audience rules diverted every
        // such user to the visitor "Create profile" form on sign-in (routeAfterAuth)
        // and they could never reach their own home. Names stay required for
        // everyone.
        if (!facts.IsVisitorProfileType)
        {
            return hasNames;
        }

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

        // ID image follows the avatar contract: magic-byte and size are
        // already checked at the endpoint. The bytes land in the
        // unified StoredFile store (App DB, owner = the user, Confidential/encrypted),
        // whose upload pipeline runs the malware scan + magic-byte allow-list +
        // canonical MIME + SHA-256 + audit — so the standalone scanner call is gone.
        // IdImageRelativePath is repurposed as the bare-Guid pointer + "has ID image"
        // presence sentinel (the completeness rule reads it null-vs-non-empty).
        var profile = await profiles.FindAsync(actorUserId, cancellationToken);
        if (profile is null)
        {
            // ID image only makes sense alongside a profile row — create
            // a stub so the pointer has somewhere to live.
            profile = new UserProfile
            {
                UserId = actorUserId,
                CreatedAt = timeProvider.SimfNow(),
            };
            profiles.Add(profile);
        }

        var priorFileId = ParseFileId(profile.IdImageRelativePath);
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.IdDocument, actorUserId, content, null, contentType, actorUserId, FailClosed: false),
            cancellationToken);
        profile.IdImageRelativePath = result.Id.ToString();
        profile.UpdatedAt = timeProvider.SimfNow();
        // UserProfile is on the App DB.
        await profiles.SaveAppChangesAsync(cancellationToken);

        // IdDocument is Secret-tier + DeletableDefault:false, so the ordinary delete
        // is refused; secure-erase the superseded scan to keep one active per owner
        // (replace-in-place, matching the legacy single-file store — but stronger).
        await RetirePriorFileAsync(priorFileId, result.Id, actorUserId, forceDelete: true, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = $"{content.Length} bytes, {contentType}; fileId={result.Id}",
        }, cancellationToken);
    }

    public async Task<UserIdDocumentImage?> ReadIdImageAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        // Owner-scoped raw decrypt read from the unified StoredFile
        // store (App DB, owner = the user). Self-read: the sub-claim gate on the
        // endpoint is the authorization; no PII audit (self-access, not a third-party
        // disclosure). AES-GCM integrity is intrinsic (a tampered blob → null).
        var locator = await profiles.GetOwnerScopedFileAsync(
            FileService.IdDocument, actorUserId, cancellationToken);
        if (locator is not { } file) { return null; }
        var bytes = await fileStorage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        return bytes is null ? null : new UserIdDocumentImage(bytes, file.ContentType ?? "application/octet-stream");
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
            // The same 404-on-mismatch policy used elsewhere — no cross-kind enumeration.
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
                CreatedAt = timeProvider.SimfNow(),
            };
            profiles.Add(profile);
        }

        // Store the bytes in the unified StoredFile store (App DB, owner
        // = the subject, Confidential/encrypted). IFileService runs the full pipeline
        // (malware scan, magic-byte allow-list, canonical MIME, SHA-256, audit), so
        // the standalone scanner call is gone. IdImageRelativePath is the bare-Guid
        // pointer + presence sentinel.
        var priorFileId = ParseFileId(profile.IdImageRelativePath);
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.IdDocument, subjectUserId, content, null, contentType, actorUserId, FailClosed: false),
            cancellationToken);
        profile.IdImageRelativePath = result.Id.ToString();
        profile.UpdatedAt = timeProvider.SimfNow();
        // UserProfile is on the App DB.
        await profiles.SaveAppChangesAsync(cancellationToken);

        // IdDocument is Secret-tier + DeletableDefault:false, so the ordinary delete
        // is refused; secure-erase the superseded scan to keep one active per owner
        // (replace-in-place, matching the legacy single-file store — but stronger).
        await RetirePriorFileAsync(priorFileId, result.Id, actorUserId, forceDelete: true, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-upload; {content.Length} bytes; {contentType}; fileId={result.Id}",
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

        // Owner-scoped raw decrypt read from the unified StoredFile store
        // (App DB, owner = the subject). The UserType guard above + the route's
        // Visitors.View gate are the authorization; AES-GCM integrity is intrinsic.
        var locator = await profiles.GetOwnerScopedFileAsync(
            FileService.IdDocument, subjectUserId, cancellationToken);
        if (locator is not { } file) { return null; }
        var bytes = await fileStorage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        if (bytes is null) { return null; }

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
            Detail = $"admin-read; {bytes.Length} bytes; {file.ContentType}",
        }, cancellationToken);

        return new UserIdDocumentImage(bytes, file.ContentType ?? "application/octet-stream");
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
                CreatedAt = timeProvider.SimfNow(),
            };
            profiles.Add(profile);
        }

        // Store the bytes in the unified StoredFile store (App DB,
        // owner = subject userId, encrypted at rest). IFileService runs the full
        // pipeline (malware scan, magic-byte allow-list, canonical MIME, SHA-256,
        // audit). VipPhotoRelativePath is repurposed as the bare-Guid pointer +
        // "has VIP photo" presence sentinel, so VipRosterService keeps working.
        var priorFileId = ParseFileId(profile.VipPhotoRelativePath);
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.VipPhoto, subjectUserId, content, null, contentType, actorUserId, FailClosed: false),
            cancellationToken);
        profile.VipPhotoRelativePath = result.Id.ToString();
        profile.UpdatedAt = timeProvider.SimfNow();
        // UserProfile is on the App DB.
        await profiles.SaveAppChangesAsync(cancellationToken);

        // Retire the prior file (best-effort — see RetirePriorFileAsync). VipPhoto is
        // DeletableDefault:true, so the ordinary soft-delete retires it.
        await RetirePriorFileAsync(priorFileId, result.Id, actorUserId, forceDelete: false, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileVipPhotoUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-upload vip-photo; {content.Length} bytes; {contentType}; fileId={result.Id}",
        }, cancellationToken);
    }

    public async Task<VipPhotoImage?> ReadVipPhotoForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        UserType expectedKind,
        CancellationToken cancellationToken = default)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken);
        if (subject is null || subject.UserType != expectedKind) { return null; }

        // Resolve the VIP photo from the unified StoredFile store
        // (App DB, owner-scoped). Raw decrypt read: the ExpectedKind guard above is
        // the authorization; the admin fetch route also gates on Visitors.View. The
        // bytes are AES-GCM encrypted at rest, so a tampered blob fails the auth tag
        // on decrypt (ReadAsync → null) — the integrity guard is intrinsic.
        var locator = await profiles.GetOwnerScopedFileAsync(
            FileService.VipPhoto, subjectUserId, cancellationToken);
        if (locator is not { } file) { return null; }
        var bytes = await fileStorage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        if (bytes is null) { return null; }

        // PII — an admin READ of a VIP welcome photo is a personal-data
        // disclosure and must leave an audit trail, mirroring the ID-image read
        // (ReadIdImageForSubjectAsync). Only the actual byte disclosure is audited —
        // a 404 for a subject with no photo on file is not.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileVipPhotoViewed,
            Outcome = AuditOutcome.Success,
            SubjectUserId = subjectUserId,
            SubjectEmail = subject.Email,
            ActorUserId = actorUserId,
            Detail = $"admin-read vip-photo; {bytes.Length} bytes; {file.ContentType}",
        }, cancellationToken);

        return new VipPhotoImage(bytes, file.ContentType ?? "image/png");
    }

    /// <summary>The VIP-photo / avatar / ID-image pointer columns
    /// hold a StoredFile GUID. Returns it when parseable, else null.</summary>
    private static Guid? ParseFileId(string? pointer) =>
        Guid.TryParse(pointer, out var id) ? id : null;

    /// <summary>Best-effort retirement of a replaced owner-scoped
    /// file so one-active-per-owner holds. The new file is already the committed
    /// source of truth, so a failure here (e.g. a stale pointer whose
    /// <c>StoredFile</c> row is gone → 404) must NOT fail the upload and trigger an
    /// orphan-spawning retry. Worst case leaves one orphaned blob for the retention
    /// sweep to reap. No-op when there is no prior file or it is the just-uploaded
    /// one. <paramref name="forceDelete"/> = true for a Secret-tier, non-deletable
    /// service (ID document): the superseded copy is <b>secure-erased</b> (DEK
    /// crypto-shred + audit), since the ordinary delete is refused by the retention
    /// hold — this preserves the legacy replace-in-place semantics.</summary>
    private async Task RetirePriorFileAsync(
        Guid? priorFileId, Guid newFileId, Guid actorUserId, bool forceDelete,
        CancellationToken cancellationToken)
    {
        if (priorFileId is not { } old || old == newFileId) { return; }
        try
        {
            if (forceDelete)
            {
                await fileService.ForceDeleteAsync(old, actorUserId, cancellationToken);
            }
            else
            {
                await fileService.DeleteAsync(old, actorUserId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Retirement of prior file {PriorFileId} failed; new file {NewFileId} is committed.",
                old, newFileId);
        }
    }

    private static UserProfileResponse ToResponse(
        UserProfile profile, string? qrId, string nationalityCode, bool hasAvatar,
        bool isVip, bool isForVisitor) =>
        new()
        {
            ProfileTypeId = profile.ProfileTypeId,
            InterestIds = profile.Interests.Select(interest => interest.Id).ToList(),
            ArabicName = profile.NameArabic,
            EnglishName = profile.Name,
            JobTitle = profile.JobTitle,
            JobTitleArabic = profile.JobTitleArabic,
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
            // Surface both renderings of the stored canonical code.
            PlateNumberAr = SaudiPlate.ToArabic(profile.PlateNumber),
            PlateNumberEn = SaudiPlate.ToEnglish(profile.PlateNumber),
            ReferenceNumber = profile.ReferenceNumber,
            OrganisationId = profile.OrganisationId,
            RegionId = profile.RegionId,
            Gender = profile.Gender,
            HasIdImage = !string.IsNullOrEmpty(profile.IdImageRelativePath),
            HasAvatar = hasAvatar,
            QrId = qrId,
            IsVip = isVip,
            // Bi-Meeting rework — the two per-user meeting-eligibility flags read
            // straight off the profile row (admin-assigned; they replace the VIP /
            // delegate gates the app used to key the meeting affordances on).
            AllowsSpeakerMeeting = profile.AllowsSpeakerMeeting,
            AllowsDelegationMeeting = profile.AllowsDelegationMeeting,
            ShowInMeetLikeYou = profile.ShowInMeetLikeYou,
            IsForVisitor = isForVisitor,
        };

    // The account's ProfileType-derived flags
    // for the app: IsVip (AllowsVipMeetingSlots, VVIP/VIP) and IsForVisitor
    // (audience vs "Other" tier). Resolved from the ProfileType in one lookup and
    // passed into ToResponse (like hasAvatar) rather than read off a nav, so a
    // freshly upserted profile whose ProfileType nav is not loaded still reports
    // them. No type assigned yet → not VIP, treated as audience (IsForVisitor true,
    // so the "show me in Meet People" opt-in stays hidden).
    private async Task<(bool IsVip, bool IsForVisitor)> ResolveProfileTypeFlagsAsync(
        Guid? profileTypeId, CancellationToken cancellationToken)
    {
        if (profileTypeId is not { } id)
        {
            return (false, true);
        }
        var profileType = await profiles.FindProfileTypeAsync(id, cancellationToken);
        return (profileType?.AllowsVipMeetingSlots ?? false,
            profileType?.IsForVisitor ?? true);
    }

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The plate is stored as the canonical Latin "code"
    /// (Latin letters + Western digits, separators stripped) via the shared
    /// <see cref="SaudiPlate"/>. The Arabic and English renderings are derived
    /// on read (no duplicated persistence) — see <see cref="ToResponse"/>.</summary>
    private static string? NormalisePlate(string? value) => SaudiPlate.Normalize(value);
}
