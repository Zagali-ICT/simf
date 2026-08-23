// Tests: SIMF.Api.Tests/AccountDeletionTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.Auditing;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>Implements <see cref="IAccountDeletionService"/>.</summary>
internal sealed class AccountDeletionService(
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    IDeviceKeyService deviceKeys,
    IFileService files,
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

    public async Task DeleteOwnAccountAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            // Idempotent by contract: nothing to erase is a success, not a 404.
            return;
        }

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

        // The identity documents are HARD-deleted, not blanked. A soft-deleted
        // row keeps its number's digest in the globally unique index, which
        // would bar that person from ever registering that document again.
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
