// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Implements account-management use cases for the signed-in user
/// (myComment #11): reading the profile, updating and removing the avatar.
/// Avatars are stored on the filesystem via <see cref="IAvatarStorage"/>
/// (D-039); the user row carries only the relative path.
/// </summary>
internal sealed class AccountService(
    UserManager<SimfUser> userManager,
    IAvatarStorage avatarStorage,
    IRecoveryCodeService recoveryCodes,
    IAuditLog auditLog,
    ILogger<AccountService> logger) : IAccountService
{
    private const int MaxAvatarSizeBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    public async Task<ProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var roles = await userManager.GetRolesAsync(user);
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
        if (!AllowedMimeTypes.Contains(normalisedContentType)
            || !MagicBytesMatch(content, normalisedContentType))
        {
            await AuditRejectedAsync(user, ErrorCodes.AvatarMimeUnsupported, cancellationToken);
            throw new ApiException(
                ErrorCodes.AvatarMimeUnsupported, 400,
                "The avatar file must be PNG, JPEG or WebP.",
                "يجب أن تكون الصورة بصيغة PNG أو JPEG أو WebP.");
        }

        var relativePath = await avatarStorage.SaveAsync(
            user.Id, content, normalisedContentType, cancellationToken);
        user.AvatarRelativePath = relativePath;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AvatarUpdated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                Detail = $"mime={normalisedContentType};size={content.Length}",
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
            await avatarStorage.DeleteAsync(user.AvatarRelativePath, cancellationToken);
            user.AvatarRelativePath = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);
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
        var user = await userManager.FindByIdAsync(userId.ToString());
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
    /// Sniffs the first few bytes and confirms they match the file-format
    /// signature the caller claimed (5-agent SEV-2.1 / IBS pattern).
    /// PNG: <c>89 50 4E 47 0D 0A 1A 0A</c>.
    /// JPEG: <c>FF D8 FF</c>.
    /// WebP: <c>52 49 46 46 ?? ?? ?? ?? 57 45 42 50</c> (RIFF…WEBP).
    /// </summary>
    private static bool MagicBytesMatch(byte[] content, string contentType) =>
        contentType switch
        {
            "image/png" => content.Length >= 8
                && content[0] == 0x89 && content[1] == 0x50
                && content[2] == 0x4E && content[3] == 0x47
                && content[4] == 0x0D && content[5] == 0x0A
                && content[6] == 0x1A && content[7] == 0x0A,
            "image/jpeg" => content.Length >= 3
                && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/webp" => content.Length >= 12
                && content[0] == 0x52 && content[1] == 0x49
                && content[2] == 0x46 && content[3] == 0x46
                && content[8] == 0x57 && content[9] == 0x45
                && content[10] == 0x42 && content[11] == 0x50,
            _ => false,
        };

    /// <summary>
    /// Builds the CP-relative URL the browser uses to fetch the avatar. The
    /// <c>?v=ticks</c> from <c>UpdatedAt</c> is the cache-buster — every avatar
    /// change moves the URL forward so the browser fetches the new bytes.
    /// </summary>
    private static string? BuildAvatarUrl(SimfUser user) =>
        string.IsNullOrEmpty(user.AvatarRelativePath)
            ? null
            : $"/account/api/avatar/{user.Id:N}?v={user.UpdatedAt?.UtcTicks ?? 0}";
}
