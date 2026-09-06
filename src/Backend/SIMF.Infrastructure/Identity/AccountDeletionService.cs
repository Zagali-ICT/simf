// Tests: SIMF.Api.Tests/AccountDeletionTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Security;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Account;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>Implements <see cref="IAccountDeletionService"/>.</summary>
internal sealed class AccountDeletionService(
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    IDeviceKeyService deviceKeys,
    IFileService files,
    IAccountCodeRepository accountCodes,
    IEmailQueue emailQueue,
    IEmailTemplateResolver emailTemplates,
    IOptions<AccountDeletionOptions> deletionOptions,
    SimfAppDbContext appDb,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AccountDeletionService> logger) : IAccountDeletionService
{
    /// The three file services whose owner is a SimfUser id. Galleries and
    /// speaker presentations are owned by other entities and are not the
    /// leaver's to erase.
    private static readonly FileService[] UserOwnedFiles =
    {
        FileService.Avatar,
        FileService.IdDocument,
        FileService.VipPhoto,
    };

    // Same shape as the biometric enrolment step-up, deliberately: one emailed
    // code, short-lived, single-use, rate-limited per account and burned after
    // a handful of wrong guesses.
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CodeRequestWindow = TimeSpan.FromHours(1);
    private const int MaxCodeRequestsPerWindow = 5;
    private const int MaxCodeAttempts = 5;

    public async Task<SendAccountDeletionCodeResponse> SendDeletionCodeAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 401,
                "Sign in again to request an account deletion code.",
                "سجّل الدخول مرة أخرى لطلب رمز حذف الحساب.");
        }

        // NOTE the divergence from the biometric step-up this otherwise mirrors:
        // it refuses a Disabled account, and its endpoint demands an approved
        // one. Neither applies here. A pending, rejected or disabled holder is
        // precisely who the deletion endpoint exists for, and gating the code
        // would let them ask to be erased and never finish.
        var now = timeProvider.SimfNow();

        var recent = await accountCodes.CountCreatedSinceAsync(
            userId, AccountCodePurpose.AccountDeletion,
            now - CodeRequestWindow, cancellationToken);
        if (recent >= MaxCodeRequestsPerWindow)
        {
            await AuditCodeRejectedAsync(userId, user.Email, "rate_limited", cancellationToken);
            throw new ApiException(
                ErrorCodes.RateLimitExceeded, 429,
                "Too many deletion codes have been requested. Try again later.",
                "تم طلب رموز حذف كثيرة. حاول مرة أخرى لاحقًا.");
        }

        // Only the newest code stays valid - consume any prior unconsumed one.
        var previous = await accountCodes.GetLatestUnconsumedAsync(
            userId, AccountCodePurpose.AccountDeletion, cancellationToken);
        if (previous is not null)
        {
            await accountCodes.TryConsumeAsync(previous.Id, now, cancellationToken);
        }

        // Only the keyed hash is stored; the plaintext is emailed and never persisted.
        var plaintext = VerificationCodeGenerator.Generate();
        await accountCodes.AddAsync(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = AccountCodePurpose.AccountDeletion,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
        }, cancellationToken);

        await emailQueue.TryEnqueueAsync(
            await emailTemplates.RenderAsync(
                EmailTemplateType.AccountDeletion, user.Email,
                EmailTokens.ForCode(plaintext, CodeLifetime), cancellationToken),
            purpose: "AccountDeletion",
            subjectEmail: user.Email,
            subjectUserId: userId,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AccountDeletionCodeIssued,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            SubjectUserId = userId,
            SubjectEmail = user.Email,
        }, cancellationToken);

        return new SendAccountDeletionCodeResponse(
            EmailMask.Mask(user.Email), (int)CodeLifetime.TotalSeconds);
    }

    /// <summary>
    /// Validates the supplied code WITHOUT consuming it, and returns the row so
    /// the caller can burn it only once the erasure has actually happened.
    /// </summary>
    /// <remarks>
    /// Deferring consumption matters: a code burned before the erase would leave
    /// a holder whose deletion half-failed with a dead code and an "incorrect"
    /// message on retry. Returns null when the gate is configured off.
    /// </remarks>
    private async Task<AccountCode?> ValidateDeletionCodeAsync(
        Guid userId, string? supplied, DateTime now, string? email,
        CancellationToken cancellationToken)
    {
        if (!deletionOptions.Value.RequireCodeForDeletion)
        {
            return null;
        }

        var stored = await accountCodes.GetLatestUnconsumedAsync(
            userId, AccountCodePurpose.AccountDeletion, cancellationToken);
        if (stored is null || string.IsNullOrWhiteSpace(supplied))
        {
            await AuditCodeRejectedAsync(userId, email, "missing", cancellationToken);
            // Worded for an OLD installed build, which sends no code at all and
            // is the only caller that reaches this in practice: the current app
            // always has a code by the time it submits.
            throw new ApiException(
                ErrorCodes.AccountDeletionCodeRequired, 403,
                "A confirmation code is required. Update the SIMF app to delete your account.",
                "يلزم رمز تأكيد. حدّث تطبيق سيمف لحذف حسابك.");
        }

        if (stored.ExpiresAt <= now)
        {
            await accountCodes.TryConsumeAsync(stored.Id, now, cancellationToken);
            await AuditCodeRejectedAsync(userId, email, "expired", cancellationToken);
            throw new ApiException(
                ErrorCodes.AccountDeletionCodeExpired, 403,
                "That code has expired. Request a new one.",
                "انتهت صلاحية الرمز. اطلب رمزًا جديدًا.");
        }

        if (!ConstantTime.Matches(AccountCodeHasher.Hash(supplied.Trim()), stored.Code))
        {
            var attempts = await accountCodes.IncrementAttemptCountAsync(
                stored.Id, cancellationToken);
            if (attempts >= MaxCodeAttempts)
            {
                await accountCodes.TryConsumeAsync(stored.Id, now, cancellationToken);
            }
            await AuditCodeRejectedAsync(userId, email, "mismatch", cancellationToken);
            throw new ApiException(
                ErrorCodes.AccountDeletionCodeInvalid, 403,
                "That code is not correct. Check it and try again.",
                "الرمز غير صحيح. تحقق منه وحاول مرة أخرى.");
        }

        return stored;
    }

    private Task AuditCodeRejectedAsync(
        Guid userId, string? email, string reason, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AccountDeletionCodeRejected,
            Outcome = AuditOutcome.Failure,
            ActorUserId = userId,
            SubjectUserId = userId,
            SubjectEmail = email,
            Detail = $"reason={reason}",
        }, cancellationToken);

    public async Task DeleteOwnAccountAsync(
        Guid userId, string? code, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            // Idempotent by contract: nothing to erase is a success, not a 404.
            return;
        }

        // The code's clock is SimfNow, matching every other AccountCode path.
        // The erasure stamps below keep GetUtcNow: sharing one "now" between the
        // two would put the expiry math out by the Saudi offset.
        var deletionCode = await ValidateDeletionCodeAsync(
            userId, code, timeProvider.SimfNow(), user.Email, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Captured BEFORE the scrub - the audit row is the record of who asked,
        // and after the next few statements these values no longer exist.
        var subjectEmail = user.Email;

        // App database FIRST, matching every other cross-database write here
        // (AdminAccountService.Approval, DormantAccountService): admission is
        // what a gate reads, so it is the half that must not be left behind.
        await ErasePersonalDataAsync(userId, now, cancellationToken);
        await EraseFilesAsync(userId, cancellationToken);

        // Identity second. Revoke before scrubbing: a live session outlives a
        // blanked column, so killing credentials is the part that must not fail
        // silently.
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);
        await deviceKeys.RevokeAllForUserAsync(user.Id, cancellationToken);
        await AnonymiseAccountAsync(user, now, cancellationToken);

        // Single-use, burned only now the erasure has committed.
        if (deletionCode is not null)
        {
            await accountCodes.TryConsumeAsync(
                deletionCode.Id, timeProvider.SimfNow(), cancellationToken);
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AccountSelfDeleted,
                Outcome = AuditOutcome.Success,
                SubjectEmail = subjectEmail,
                SubjectUserId = userId,
                ActorUserId = userId,
                Detail = "Self-service account deletion (Google Play requirement).",
            },
            cancellationToken);

        logger.LogInformation(
            "Account {UserId} erased at the holder's request.", userId);
    }

    /// <summary>Scrubs the attendee profile and withdraws admission.</summary>
    private async Task ErasePersonalDataAsync(
        Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var profile = await appDb.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            // Signed up, never completed registration. There is no attendee
            // record to erase - the Identity half below is the whole job.
            return;
        }

        // The identity documents are HARD-deleted, not blanked. The original
        // reason was the globally unique digest index, which a retired row would
        // have kept occupying; that index is gone. The behaviour stays for the
        // reason that outlived it: this is an ERASURE request, and a blanked row
        // that still carries the encrypted number has not erased anything.
        var documents = await appDb.Set<ProfileIdentityDocument>()
            .Where(d => d.ProfileId == profile.Id)
            .ToListAsync(cancellationToken);
        appDb.Set<ProfileIdentityDocument>().RemoveRange(documents);

        profile.Name = DeletedPlaceholder;
        profile.NameArabic = DeletedPlaceholder;
        profile.PlaceOfBirth = string.Empty;
        profile.DateOfBirth = null;
        profile.Gender = Gender.Unspecified;
        profile.JobTitle = null;
        profile.JobTitleArabic = null;
        profile.Honorific = null;
        profile.HonorificArabic = null;
        profile.MobileNumber = null;
        profile.SaudiMobile = null;
        profile.InternationalMobile = null;
        profile.PlateNumber = null;
        profile.MawjId = null;
        profile.OrganisationId = null;

        // QrId carries a filtered UNIQUE index and is the badge a gate scans.
        // Leaving it would admit the holder after they asked to be erased.
        profile.QrId = null;

        profile.AdmissionState = AccountState.Disabled;
        profile.StateChangedAt = now;
        profile.StateChangedByUserId = userId;
        profile.Deactivate();

        await appDb.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Securely destroys the ID scan, avatar and VIP photo.</summary>
    private async Task EraseFilesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var fileIds = await appDb.StoredFiles
            .Where(f => f.OwnerEntityId == userId
                && UserOwnedFiles.Contains(f.Service)
                && f.IsActive)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        foreach (var fileId in fileIds)
        {
            // ForceDelete, not Delete: the ID document is Secret-tier and
            // carries a retention hold that refuses the ordinary delete. These
            // are an identity scan and a face image - crypto-shredding them is
            // the point of the feature, not an optimisation.
            await files.ForceDeleteAsync(fileId, userId, cancellationToken);
        }

        // The file store does not clear an owner's pointers for these three
        // services (OwnerPointerSync has no case for them), so the caller must.
        await appDb.UserProfiles
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.IdImageFileId, (Guid?)null)
                    .SetProperty(p => p.VipPhotoFileId, (Guid?)null),
                cancellationToken);
    }

    /// <summary>Blanks the credential row and disables sign-in.</summary>
    private async Task AnonymiseAccountAsync(
        Domain.IdentityAccess.SimfUser user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // NormalizedEmail and UserName both carry unique indexes, so the
        // replacement has to stay unique - derive it from the id rather than
        // using a shared literal, or the second deletion collides with the first.
        var tombstone = $"deleted+{user.Id:N}@invalid";

        user.Email = tombstone;
        user.NormalizedEmail = tombstone.ToUpperInvariant();
        user.UserName = tombstone;
        user.NormalizedUserName = tombstone.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.TwoFactorEnabled = false;
        // DisplayName is required and max-length 256 - it cannot be nulled.
        user.DisplayName = DeletedPlaceholder;
        user.AvatarFileId = null;
        user.AccountState = AccountState.Disabled;
        user.StateChangedAt = now;
        user.StateChangedByUserId = user.Id;
        user.UpdatedAt = now;

        var result = await accounts.UpdateAsync(user, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not erase the account: "
                + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // Invalidates every access token already issued - without this the
        // holder stays signed in until their current token expires.
        await accounts.UpdateSecurityStampAsync(user, cancellationToken);
    }

    private const string DeletedPlaceholder = "Deleted account";
}
