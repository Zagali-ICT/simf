// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
//        SIMF.Api.Tests/UserProfileTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Implements account-management use cases for the signed-in user:
/// reading the profile, updating and removing the avatar.
/// Avatar bytes now live in the unified <see cref="IFileService"/>
/// store (App DB, <c>FileService.Avatar</c>, encrypted at rest). The Identity
/// <c>SimfUser.AvatarRelativePath</c> column is repurposed as the bare-Guid pointer
/// to that <c>StoredFile</c> (never a cross-DB FK) and doubles as the "has
/// avatar" presence sentinel that <see cref="BuildAvatarUrl"/> and the profile-
/// completeness / male-face gates already read; so those Identity-side reads stay
/// byte-identical and no Identity migration is needed.
/// </summary>
internal sealed class AccountService(
    IUserAccountRepository accounts,
    IFileService fileService,
    IRecoveryCodeService recoveryCodes,
    IUserProfileService userProfiles,
    IAuditLog auditLog,
    ILogger<AccountService> logger) : IAccountService
{
    private const int MaxAvatarSizeBytes = 2 * 1024 * 1024;

    public async Task<ProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var roles = await accounts.GetRolesAsync(user);
        var remaining = await recoveryCodes.CountActiveAsync(user.Id, cancellationToken);

        return new ProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            BuildAvatarUrl(user),
            user.TwoFactorEnabled,
            remaining,
            [.. roles]);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        // The app-role lookup reads the App database (ProfileType.MobileAppRole)
        // — a second query on the other context, never a cross-DB join.
        var mobileAppRole = await userProfiles.ResolveMobileAppRoleAsync(user.Id, cancellationToken);
        // Completeness rides on the login flow so the app can force
        // the add-profile stage without a separate profile probe.
        var profileComplete = await userProfiles.IsProfileCompleteAsync(user.Id, cancellationToken);

        return new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            MapAppRole(mobileAppRole),
            // The Identity row carries no per-user language today; Arabic is the
            // primary / default language, and the app overrides
            // it locally. Emitted for wire-shape completeness.
            PreferredLanguageDefault,
            MapRegistrationStatus(user.AccountState),
            BuildAvatarUrl(user),
            profileComplete);
    }

    public async Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var codes = await recoveryCodes.GenerateAsync(user.Id, cancellationToken);
        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.TotpRecoveryCodesRegenerated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);
        logger.LogInformation("Recovery codes regenerated for {Email}", user.Email);
        return new RecoveryCodesResponse(codes);
    }

    public async Task<AvatarResponse> SetAvatarAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        if (content.Length == 0)
        {
            await AuditRejectedAsync(user, ErrorCodes.AvatarFileMissing, cancellationToken);
            throw new ApiException(
                ErrorCodes.AvatarFileMissing, 400,
                "An avatar file is required.",
                "ملف الصورة مطلوب.");
        }

        if (content.Length > MaxAvatarSizeBytes)
        {
            await AuditRejectedAsync(user, ErrorCodes.AvatarFileTooLarge, cancellationToken);
            throw new ApiException(
                ErrorCodes.AvatarFileTooLarge, 400,
                "The avatar file must be 2 MB or less.",
                "يجب ألا يتجاوز حجم الصورة 2 ميغابايت.");
        }

        var normalisedContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ImageUploadValidation.IsAllowedImage(content, normalisedContentType))
        {
            await AuditRejectedAsync(user, ErrorCodes.AvatarMimeUnsupported, cancellationToken);
            throw new ApiException(
                ErrorCodes.AvatarMimeUnsupported, 400,
                "The avatar file must be PNG, JPEG or WebP.",
                "يجب أن تكون الصورة بصيغة PNG أو JPEG أو WebP.");
        }

        // Store the bytes in the unified StoredFile store (App DB,
        // owner = the user id). IFileService runs the full pipeline (malware scan,
        // magic-byte allow-list, canonical MIME, SHA-256, encrypt-at-rest, audit);
        // the avatar-specific 2 MB cap + MIME check above still apply first.
        var priorFileId = ParseFileId(user.AvatarRelativePath);
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.Avatar, user.Id, content, null, normalisedContentType, user.Id, FailClosed: false),
            cancellationToken);

        user.AvatarRelativePath = result.Id.ToString();
        user.UpdatedAt = SimfClock.Now;
        await accounts.UpdateAsync(user).EnsureSuccessAsync();

        // Retire the replaced avatar's bytes (one active per user) after the
        // pointer is safely repointed.
        if (priorFileId is { } old && old != result.Id)
        {
            await fileService.DeleteAsync(old, user.Id, cancellationToken);
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AvatarUpdated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                Detail = $"mime={normalisedContentType};size={content.Length};fileId={result.Id}",
            },
            cancellationToken);

        logger.LogInformation("Avatar updated for {Email}", user.Email);
        return new AvatarResponse(BuildAvatarUrl(user));
    }

    public async Task<AvatarResponse> RemoveAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        if (!string.IsNullOrEmpty(user.AvatarRelativePath))
        {
            // Securely delete the StoredFile the pointer references.
            if (ParseFileId(user.AvatarRelativePath) is { } fileId)
            {
                await fileService.DeleteAsync(fileId, user.Id, cancellationToken);
            }
            user.AvatarRelativePath = null;
            user.UpdatedAt = SimfClock.Now;
            await accounts.UpdateAsync(user).EnsureSuccessAsync();
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AvatarUpdated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                Detail = "removed",
            },
            cancellationToken);

        logger.LogInformation("Avatar removed for {Email}", user.Email);
        return new AvatarResponse(null);
    }

    private async Task<SimfUser> GetUserAsync(Guid userId)
    {
        var user = await accounts.FindByIdAsync(userId);
        return user ?? throw new ApiException(
            ErrorCodes.AuthAccountNotFound, 404,
            "The account was not found.",
            "لم يتم العثور على الحساب.");
    }

    private Task AuditRejectedAsync(SimfUser user, string errorCode, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AvatarRejected,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                ErrorCode = errorCode,
            },
            cancellationToken);

    /// <summary>
    /// Builds the CP-relative URL the browser uses to fetch the avatar. The
    /// <c>?v=ticks</c> from <c>UpdatedAt</c> is the cache-buster — every avatar
    /// change moves the URL forward so the browser fetches the new bytes.
    /// </summary>
    private static string? BuildAvatarUrl(SimfUser user) =>
        string.IsNullOrEmpty(user.AvatarRelativePath)
            ? null
            : $"/account/api/avatar/{user.Id:N}?v={user.UpdatedAt?.Ticks ?? 0}";

    /// <summary>The avatar pointer (<c>AvatarRelativePath</c>) now
    /// holds the StoredFile GUID. Returns it when parseable, else null (a legacy
    /// non-Guid path, or unset).</summary>
    private static Guid? ParseFileId(string? pointer) =>
        Guid.TryParse(pointer, out var id) ? id : null;

    /// <summary>The primary-language default emitted for <c>users/me</c> until
    /// a per-user language preference is persisted.</summary>
    private const string PreferredLanguageDefault = "ar";

    /// <summary>
    /// Maps the resolved <see cref="MobileAppRole"/> to the wire name the app's
    /// <c>AppRole</c> enum decodes. <see cref="MobileAppRole.None"/> means
    /// "treated as a Visitor by the Flutter app" (partner profile types / admins)
    /// — emit the effective floor so an authenticated user is never read back as
    /// Guest. The string form is used so the integer drift between the backend and
    /// app role enums never matters.
    /// </summary>
    private static string MapAppRole(MobileAppRole role) =>
        role == MobileAppRole.None ? nameof(MobileAppRole.Visitor) : role.ToString();

    /// <summary>
    /// Collapses the six-value <see cref="AccountState"/> onto the app's
    /// three-value registration vocabulary: the pre-approval states all read as
    /// <c>Pending</c>; <c>Disabled</c> cannot proceed, so it reads as
    /// <c>Rejected</c>.
    /// </summary>
    private static string MapRegistrationStatus(AccountState state) => state switch
    {
        AccountState.Approved => "Approved",
        AccountState.Rejected => "Rejected",
        AccountState.Disabled => "Rejected",
        _ => "Pending",
    };
}
