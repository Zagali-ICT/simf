// Tests: SIMF.Api.Tests/UserProfileTests.cs (upsert round-trip, ID image
//        round-trip, get-empty-when-not-saved-yet, nationality-unknown)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Notifications;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// User self-service profile + encrypted ID-document storage (decisions
/// D-046 b, P8 — D-049; renamed from <c>VisitorProfileService</c>). The
/// actor identity is taken from the access token (the endpoint resolves
/// <c>sub</c>); every call operates on the actor's own row, so the
/// service does not need an admin-vs-self check.
/// </summary>
internal sealed class UserProfileService(
    IUserAccountRepository accounts,
    SimfIdentityDbContext dbContext,
    IUserIdDocumentStorage idStorage,
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

        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .Include(p => p.Interests)
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);

        if (profile is null)
        {
            // Empty response — the user has not filled the form yet. The
            // QR id lives on the profile now (D-106), so when no profile
            // row exists yet the QR isn't available either; the page
            // will render the empty form without a QR until the user
            // saves the form.
            return new UserProfileResponse();
        }

        return ToResponse(profile, profile.QrId);
    }

    public async Task<UserProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the nationality against the curated list — an unmatched
        // code is rejected here even though the validator already checks.
        // (Defence in depth — a future caller that bypasses the validator
        // still cannot persist garbage.)
        if (!Countries.IsKnown(request.NationalityCode))
        {
            throw new DataValidationException(
                $"Nationality code '{request.NationalityCode}' is not supported.",
                $"الجنسية '{request.NationalityCode}' غير مدعومة.");
        }

        var user = await accounts.FindByIdAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        // P9 — validate the picked interest ids: every id must exist
        // and be active. (The validator already enforces 1-10 count.)
        var requestedIds = request.InterestIds.Distinct().ToList();
        var foundActiveIds = await dbContext.Interests
            .AsNoTracking()
            .Where(interest => requestedIds.Contains(interest.Id) && interest.IsActive)
            .Select(interest => interest.Id)
            .ToListAsync(cancellationToken);
        if (foundActiveIds.Count != requestedIds.Count)
        {
            throw new ApiException(
                ErrorCodes.InterestInvalid, 400,
                "One or more selected interests are unknown or no longer active.",
                "بعض الاهتمامات المختارة غير معروفة أو لم تعد مفعّلة.");
        }

        var now = timeProvider.GetUtcNow();
        var profile = await dbContext.UserProfiles
            .Include(p => p.Interests)
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);

        var isNew = profile is null;
        // P8 — the admin may have created a stub row with a ProfileTypeId
        // already set (e.g. via /admin/others). Preserve it; the user
        // cannot self-pick a profile type on the upsert.
        profile ??= new UserProfile { UserId = actorUserId, CreatedAt = now };

        profile.ArabicName = request.ArabicName;
        profile.EnglishName = request.EnglishName;
        profile.NationalityCode = request.NationalityCode.ToUpperInvariant();
        profile.DateOfBirth = request.DateOfBirth;
        profile.PlaceOfBirth = request.PlaceOfBirth;
        profile.IsSaudi = request.IsSaudi;
        profile.NationalId = request.IsSaudi ? request.NationalId : null;
        profile.IqamaNumber = request.IsSaudi ? null : request.IqamaNumber;
        profile.PassportNumber = request.IsSaudi ? null : request.PassportNumber;
        profile.SaudiMobile = NormaliseOptional(request.SaudiMobile);
        profile.InternationalMobile = NormaliseOptional(request.InternationalMobile);
        if (!isNew)
        {
            profile.UpdatedAt = now;
        }

        if (isNew)
        {
            dbContext.UserProfiles.Add(profile);
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
            var freshRows = await dbContext.Interests
                .Where(interest => toAddIds.Contains(interest.Id))
                .ToListAsync(cancellationToken);
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
        var transitioned = false;
        await transactionRunner.ExecuteAsync(async token =>
        {
            await dbContext.SaveChangesAsync(token);

            if (isNew && user.AccountState == AccountState.EmailVerified)
            {
                user.AccountState = AccountState.PendingApproval;
                user.StateChangedAt = now;
                user.StateChangedByUserId = null;
                var updateResult = await accounts.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Auto-transition to PendingApproval failed: " +
                        string.Join("; ", updateResult.Errors.Select(error => error.Description)));
                }

                // Stale tokens still encode the old account_state claim;
                // revoke them so the user has to sign in again and the
                // next JWT reflects PendingApproval.
                await refreshTokens.RevokeAllForUserAsync(actorUserId, now, token);
                transitioned = true;
            }
        }, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileSaved,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = isNew ? "created" : "updated",
        }, cancellationToken);

        logger.LogInformation(
            "User profile {Operation} for {UserId}",
            isNew ? "created" : "updated", actorUserId);

        if (transitioned)
        {
            await DispatchProfileSubmittedAsync(user, cancellationToken);
            await DispatchAdminPendingVisitorAsync(user, cancellationToken);
        }

        return ToResponse(profile, profile.QrId);
    }

    /// <summary>
    /// D-106: implements <see cref="IUserProfileService.GetRejectionTextAsync"/>.
    /// Reads the bilingual rejection text directly from UserProfile; the
    /// SignInService uses this for the AccountStateInfo state-banner.
    /// </summary>
    public async Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.RejectionReason, p.RejectionReasonArabic })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) { return null; }
        if (row.RejectionReason is null && row.RejectionReasonArabic is null) { return null; }
        return new RejectionText(row.RejectionReason, row.RejectionReasonArabic);
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
        var admins = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Admin && u.AccountState == AccountState.Approved)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(cancellationToken);

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

        // ID image follows the avatar contract (D-039): magic-byte and
        // size already checked at the endpoint, the storage layer
        // encrypts and writes.
        var profile = await dbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);
        if (profile is null)
        {
            // ID image only makes sense alongside a profile row — create
            // a stub so the relative path has somewhere to live.
            profile = new UserProfile
            {
                UserId = actorUserId,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            dbContext.UserProfiles.Add(profile);
        }

        var relativePath = await idStorage.SaveAsync(
            actorUserId, content, contentType, cancellationToken);
        profile.IdImageRelativePath = relativePath;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

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
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);
        if (profile is null || string.IsNullOrEmpty(profile.IdImageRelativePath))
        {
            return null;
        }
        var read = await idStorage.OpenReadAsync(profile.IdImageRelativePath, cancellationToken);
        return read is null ? null : new UserIdDocumentImage(read.Content, read.ContentType);
    }

    private static UserProfileResponse ToResponse(UserProfile profile, string? qrId) =>
        new()
        {
            ProfileTypeId = profile.ProfileTypeId,
            InterestIds = profile.Interests.Select(interest => interest.Id).ToList(),
            ArabicName = profile.ArabicName,
            EnglishName = profile.EnglishName,
            NationalityCode = profile.NationalityCode,
            DateOfBirth = profile.DateOfBirth,
            PlaceOfBirth = profile.PlaceOfBirth,
            IsSaudi = profile.IsSaudi,
            NationalId = profile.NationalId,
            IqamaNumber = profile.IqamaNumber,
            PassportNumber = profile.PassportNumber,
            SaudiMobile = profile.SaudiMobile,
            InternationalMobile = profile.InternationalMobile,
            HasIdImage = !string.IsNullOrEmpty(profile.IdImageRelativePath),
            QrId = qrId,
        };

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
