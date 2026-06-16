namespace SIMF.Application.Abstractions;

/// <summary>
/// V-1 (D-429) — stores and retrieves the VVIP/VIP welcome photo (صورة واضحة).
/// A store separate from <see cref="IAvatarStorage"/>: the VIP photo is a
/// distinct, high-resolution portrait used by the موج (Mawj) welcome-message
/// export, and both stores key files by user id, so sharing one store would let
/// the VIP photo overwrite the account avatar. The default implementation writes
/// to the local filesystem under the configured <c>Storage:VipPhotoBase</c> path;
/// the interface keeps the higher layers testable. Reuses <see cref="AvatarRead"/>
/// for the open-read result (a generic stream + content-type pair).
/// </summary>
public interface IVipPhotoStorage
{
    /// <summary>
    /// Stores the VIP photo for the user. Any previous photo for the same user
    /// is replaced atomically. Returns the new relative path to persist on the
    /// profile row's <c>VipPhotoRelativePath</c>.
    /// </summary>
    /// <param name="userId">The account the photo belongs to. Used as the file name.</param>
    /// <param name="content">The validated image bytes.</param>
    /// <param name="contentType">The validated MIME type (image/png, image/jpeg, image/webp).</param>
    /// <returns>The path, relative to the storage root, of the new file.</returns>
    Task<string> SaveAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the VIP photo at the given relative path. Idempotent.</summary>
    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream over the VIP photo at the given relative path,
    /// along with its MIME content type. Returns <c>null</c> if the file is
    /// missing (treated as "no photo").
    /// </summary>
    Task<AvatarRead?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
