// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (Admin_uploads_vip_photo_sets_path)
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// V-1 (D-429) — file-system implementation of <see cref="IVipPhotoStorage"/>.
/// A store separate from the avatar (both key files by user id, so sharing one
/// directory would let the VIP photo overwrite the account avatar). The keyed-
/// file storage + atomic-write + path-safety logic lives in the shared
/// <see cref="FilesystemImageStore"/> base (D-430 follow-up); only the base
/// directory differs.
/// </summary>
internal sealed class FilesystemVipPhotoStorage : FilesystemImageStore, IVipPhotoStorage
{
    public FilesystemVipPhotoStorage(
        IOptions<StorageOptions> options,
        ILogger<FilesystemVipPhotoStorage> logger)
        : base(ResolveBaseDirectory(options.Value), "VIP photo", logger)
    {
    }

    // Prefer the explicit VipPhotoBase; when unset (e.g. a live deploy that
    // predates this key) fall back to a "vip-photos" sibling of AvatarBase so the
    // API still boots with the VIP store on a separate directory from avatars.
    // Only when neither is configured do we fail loudly.
    private static string ResolveBaseDirectory(StorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.VipPhotoBase))
        {
            return options.VipPhotoBase;
        }
        if (!string.IsNullOrWhiteSpace(options.AvatarBase))
        {
            var avatarParent = Path.GetDirectoryName(Path.GetFullPath(options.AvatarBase));
            if (avatarParent is not null)
            {
                return Path.Combine(avatarParent, "vip-photos");
            }
        }
        throw new InvalidOperationException(
            "Configuration value 'Storage:VipPhotoBase' (or 'Storage:AvatarBase' "
            + "to derive it) is required but was not found.");
    }
}
