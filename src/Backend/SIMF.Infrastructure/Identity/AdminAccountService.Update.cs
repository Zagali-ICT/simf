// Tests: SIMF.Api.Tests/AdminAccountMobileTests.cs (the optional mobile
//        correction, stored canonicalised in the one collapsed column)
// Tests: SIMF.Api.Tests/AdminAccountNationalityTests.cs (nationality)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Per-user Edit for Visitor / Other accounts, replacing what used to be a
/// CP edit stub. Mirrors the create path
/// (<c>CreateAccountAsync</c>): same scope guard, same email-uniqueness and
/// profile-type validation, the same two-context save. Adds the identity-change
/// protection a create does not need: when the login email changes the security
/// stamp is rolled and the subject's refresh tokens are revoked, so a stale
/// session cannot keep signing in under the old identity.
/// </summary>
internal sealed partial class AdminAccountService
{
    public Task UpdateVisitorAsync(
        Guid actorUserId, Guid userId, AdminUpdateVisitorRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAccountAsync(
            actorUserId, userId, request.Email, request.DisplayName,
            request.ProfileTypeId, expectedIsVisitor: true,
            profileTypeRequired: false,
            request.AllowsSpeakerMeeting, request.AllowsDelegationMeeting,
            request.NationalityCode,
            request.SaudiMobile, request.InternationalMobile,
            cancellationToken);

    public Task UpdateOtherAsync(
        Guid actorUserId, Guid userId, AdminUpdateOtherRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAccountAsync(
            actorUserId, userId, request.Email, request.DisplayName,
            request.ProfileTypeId, expectedIsVisitor: false,
            profileTypeRequired: true,
            request.AllowsSpeakerMeeting, request.AllowsDelegationMeeting,
            request.NationalityCode,
            request.SaudiMobile, request.InternationalMobile,
            cancellationToken);

    private async Task UpdateAccountAsync(
        Guid actorUserId, Guid userId, string email, string displayName,
        Guid? profileTypeId, bool expectedIsVisitor, bool profileTypeRequired,
        bool allowsSpeakerMeeting, bool allowsDelegationMeeting,
        string? nationalityCode,
        string? saudiMobile, string? internationalMobile,
        CancellationToken cancellationToken)
    {
        var trimmedEmail = (email ?? string.Empty).Trim();
        var trimmedName = (displayName ?? string.Empty).Trim();

        // Load the subject. A wrong-scope subject (e.g. an Other edited via the
        // Visitors desk) is reported as the same 404 as a missing id so the
        // desk cannot probe across scopes — identical to the approval path.
        var target = await accounts.FindByIdAsync(userId, cancellationToken);
        var scopeOk = target is not null
            && target.UserType == UserType.Visitor
            && await SubjectMatchesProfileScopeAsync(
                target.Id, expectedIsVisitor, cancellationToken);
        if (target is null || !scopeOk)
        {
            await AuditFailure(
                AuditEvents.AdminUserUpdateFailed, actorUserId, trimmedEmail,
                userId, ErrorCodes.AdminUserNotFound, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The account was not found.",
                "لم يتم العثور على الحساب.");
        }

        // Email uniqueness — re-check, but a hit on the subject's own row is
        // fine (an edit that leaves the email unchanged must succeed).
        var existing = await accounts.FindByEmailAsync(trimmedEmail, cancellationToken);
        if (existing is not null && existing.Id != target.Id)
        {
            await AuditFailure(
                AuditEvents.AdminUserUpdateFailed, actorUserId, trimmedEmail,
                target.Id, ErrorCodes.AdminEmailAlreadyRegistered, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminEmailAlreadyRegistered, 409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        // Validate the chosen profile type (existence + active + UserType +
        // audience/partner scope). Mirrors the create-time guard. A required
        // type that is missing/empty is rejected before any write.
        var resolvedProfileTypeId = await ResolveEditProfileTypeAsync(
            actorUserId, trimmedEmail, target.Id, profileTypeId,
            expectedIsVisitor, profileTypeRequired, cancellationToken);

        // Resolve the optional nationality correction BEFORE any write, with the
        // same rule (an ACTIVE Countries row, matched on the ISO alpha-2 code) and the
        // same error code the self-service upsert uses. Nationality gates delegation
        // -meeting confirm eligibility, so without this an admin had no way to fix a
        // delegate whose nationality was wrong. null = "leave it as it is".
        var resolvedNationalityId = await ResolveEditNationalityAsync(
            actorUserId, trimmedEmail, target.Id, nationalityCode, cancellationToken);

        // The optional mobile correction, reduced to null/not-null HERE so the
        // "was anything supplied?" question below (and the audit line) reads the
        // same value the write does; a blank string is "not supplied", not a
        // blanking, because the mobile is mandatory. The canonical form itself is
        // settled once, inside ProfileMobileStorage.Sync.
        var normalisedSaudiMobile = MobileNumber.NormalizeOptional(saudiMobile);
        var normalisedInternationalMobile =
            MobileNumber.NormalizeOptional(internationalMobile);

        var emailChanged = !string.Equals(
            target.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase);

        // A ProfileType change is a PRIVILEGE change: the type's
        // MobileAppRole now sources the app's operational perm claims, so a
        // demotion (e.g. Moderator → a no-authority type) must invalidate any
        // live access token — otherwise the old perms survive until the token
        // expires. Detect it here so the stamp roll below covers it exactly like
        // an email change.
        var currentProfileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == target.Id)
            .Select(profile => profile.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        var profileTypeChanged = currentProfileTypeId != resolvedProfileTypeId;

        var now = timeProvider.SimfNow();

        await transactionRunner.ExecuteAsync(async (innerCt) =>
        {
            target.Email = trimmedEmail;
            target.UserName = trimmedEmail;
            target.DisplayName = trimmedName;
            target.UpdatedAt = now;
            // An admin correcting a login email (the new-account typo case)
            // is not the account holder, so the corrected address is unverified
            // until the owner proves it. Mark it unconfirmed: sign-in gates on
            // AccountState (not EmailConfirmed), so this is not a lockout, and the
            // next sign-in's email-OTP 2FA goes to the new address — re-verifying
            // deliverability instead of trusting the typed-in correction.
            if (emailChanged)
            {
                target.EmailConfirmed = false;
            }
            var updateResult = await accounts.UpdateAsync(target);
            if (!updateResult.Succeeded)
            {
                throw new ApiException(
                    ErrorCodes.InternalError, 500,
                    "The account could not be updated.",
                    "تعذّر تحديث الحساب.");
            }

            // A login-email change OR a profile-type change is an identity /
            // privilege change: roll the stamp and revoke sessions so a stale
            // session cannot keep the old identity, and so a demoted user loses
            // their elevated app authority at the next request instead of when
            // the access token happens to expire.
            if (emailChanged || profileTypeChanged)
            {
                await accounts.UpdateSecurityStampAsync(target);
                await refreshTokenRepository.RevokeAllForUserAsync(
                    target.Id, now, innerCt);
            }

            await UpsertProfileTypeAsync(
                target.Id, resolvedProfileTypeId,
                allowsSpeakerMeeting, allowsDelegationMeeting,
                resolvedNationalityId,
                normalisedSaudiMobile, normalisedInternationalMobile,
                now, innerCt);

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.AdminUserUpdated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = trimmedEmail,
                SubjectUserId = target.Id,
                ActorUserId = actorUserId,
                Detail = $"scope={(expectedIsVisitor ? "visitor" : "other")}; "
                    + $"emailChanged={emailChanged}; "
                    + $"profileTypeChanged={profileTypeChanged}; "
                    + $"profileType={resolvedProfileTypeId}; "
                    + $"nationalityId={resolvedNationalityId?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) ?? "unchanged"}; "
                    // A phone correction is a contact-detail change on
                    // someone else's account, so the trail records THAT it happened.
                    // The number itself is not written to the audit detail (the
                    // RowAudit interceptor already masks the mobile columns).
                    + $"mobileChanged={normalisedSaudiMobile is not null
                        || normalisedInternationalMobile is not null}",
            }, innerCt);
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} updated account {SubjectId} (emailChanged={EmailChanged})",
            actorUserId, target.Id, emailChanged);
    }

    // Validates the chosen ProfileType for an edit and returns the id to
    // persist (null when none supplied and none required). Mirrors the
    // create-time checks in CreateAccountAsync.
    private async Task<Guid?> ResolveEditProfileTypeAsync(
        Guid actorUserId, string email, Guid subjectId, Guid? profileTypeId,
        bool expectedIsVisitor, bool profileTypeRequired,
        CancellationToken cancellationToken)
    {
        if (profileTypeId is null || profileTypeId == Guid.Empty)
        {
            if (profileTypeRequired)
            {
                await AuditFailure(
                    AuditEvents.AdminUserUpdateFailed, actorUserId, email,
                    subjectId, ErrorCodes.AdminProfileTypeInvalid, cancellationToken);
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "A profile type is required.",
                    "نوع الملف الشخصي مطلوب.");
            }
            return null;
        }

        var profileType = await appDbContext.ProfileTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == profileTypeId.Value, cancellationToken);
        if (profileType is null
            || !profileType.IsActive
            || profileType.IsForVisitor != expectedIsVisitor)
        {
            await AuditFailure(
                AuditEvents.AdminUserUpdateFailed, actorUserId, email,
                subjectId, ErrorCodes.AdminProfileTypeInvalid, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 400,
                "The selected profile type is not valid for this account.",
                "نوع الملف الشخصي المحدّد غير صالح لهذا الحساب.");
        }
        return profileType.Id;
    }

    // Resolves the optional nationality code an admin edit may carry.
    // Returns null when the caller omitted it (leave the stored value alone), or the
    // Country PK when it names an active country. An unknown / inactive code is the
    // same 400 (ProfileNationalityUnknown) the self-service upsert raises, so the
    // admin desk and the app agree on what a valid nationality is.
    private async Task<int?> ResolveEditNationalityAsync(
        Guid actorUserId, string email, Guid subjectId, string? nationalityCode,
        CancellationToken cancellationToken)
    {
        var code = (nationalityCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return null;
        }

        var countryId = await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Code == code && country.IsActive)
            .Select(country => (int?)country.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (countryId is null)
        {
            await AuditFailure(
                AuditEvents.AdminUserUpdateFailed, actorUserId, email,
                subjectId, ErrorCodes.ProfileNationalityUnknown, cancellationToken);
            throw new ApiException(
                ErrorCodes.ProfileNationalityUnknown, 400,
                $"Nationality code '{code}' is not supported.",
                $"الجنسية '{code}' غير مدعومة.");
        }
        return countryId;
    }

    // Sets the subject's ProfileTypeId, the two Bi-Meeting eligibility flags
    // (AllowsSpeakerMeeting / AllowsDelegationMeeting) and — when the edit supplied
    // them — the nationality and the mobile numbers, on the
    // App-DB UserProfile row. The row may not exist yet (a self-signed-up visitor
    // with no admin-assigned type); create a minimal row when a tier OR a meeting
    // flag OR a nationality OR a mobile is set so the assignment sticks. An edit
    // that changes none of those on a profile-less account stays a no-op (nothing
    // to persist).
    private async Task UpsertProfileTypeAsync(
        Guid subjectId, Guid? profileTypeId,
        bool allowsSpeakerMeeting, bool allowsDelegationMeeting,
        int? nationalityId,
        string? saudiMobile, string? internationalMobile,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var profile = await appDbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == subjectId, cancellationToken);
        if (profile is null)
        {
            if (profileTypeId is null && !allowsSpeakerMeeting
                && !allowsDelegationMeeting && nationalityId is null
                && saudiMobile is null && internationalMobile is null)
            {
                return;
            }
            var created = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = subjectId,
                ProfileTypeId = profileTypeId,
                AllowsSpeakerMeeting = allowsSpeakerMeeting,
                AllowsDelegationMeeting = allowsDelegationMeeting,
                NationalityId = nationalityId ?? 0,
                CreatedAt = now,
            };
            ProfileMobileStorage.Sync(created, saudiMobile, internationalMobile);
            appDbContext.UserProfiles.Add(created);
        }
        else
        {
            profile.ProfileTypeId = profileTypeId;
            profile.AllowsSpeakerMeeting = allowsSpeakerMeeting;
            profile.AllowsDelegationMeeting = allowsDelegationMeeting;
            if (nationalityId is { } resolved)
            {
                profile.NationalityId = resolved;
            }
            // "null = no change" is now per-ATTRIBUTE, not per-column, because the
            // mobile is one attribute: an edit that supplies neither number leaves
            // the row alone (a desk fixing only the email never wipes the contact
            // detail), and an edit that supplies EITHER number replaces the stored
            // one outright.
            //
            // It deliberately does NOT coalesce a supplied international number
            // against the stored Saudi one. That is what used to leave the row
            // holding two different numbers with nothing saying which to ring —
            // and since blanking is forbidden (the mobile is mandatory), coalescing
            // would make moving a Saudi attendee onto a foreign number impossible.
            if (saudiMobile is not null || internationalMobile is not null)
            {
                ProfileMobileStorage.Sync(profile, saudiMobile, internationalMobile);
            }
            profile.UpdatedAt = now;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);
    }
}
