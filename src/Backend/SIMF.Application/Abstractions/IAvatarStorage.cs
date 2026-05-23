namespace SIMF.Application.Abstractions;

/// <summary>
/// Stores and retrieves user-avatar files (myComment item #11, decision D-039).
/// The default implementation writes to the local filesystem under the
/// configured <c>Storage:AvatarBase</c> path; the interface keeps the higher
/// layers testable and lets a future implementation switch to object storage
/// without touching <see cref="SIMF.Application.IdentityAccess.IAccountService"/>.
/// </summary>
public interface IAvatarStorage
{
    /// <summary>
    /// Stores the avatar for the user. Any previous avatar file for the same
    /// user is replaced atomically. Returns the new relative path to persist
    /// on the user row.
    /// </summary>
    /// <param name="userId">The account the avatar belongs to. Used as the file name.</param>
    /// <param name="content">The validated image bytes.</param>
    /// <param name="contentType">The validated MIME type (image/png, image/jpeg, image/webp).</param>
    /// <returns>The path, relative to the storage root, of the new file.</returns>
    Task<string> SaveAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the avatar file at the given relative path. Idempotent.</summary>
    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream over the avatar at the given relative path,
    /// along with its MIME content type. Returns <c>null</c> if the file is
    /// missing (treated as "no avatar").
    /// </summary>
    Task<AvatarRead?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

/// <summary>An open avatar read — caller owns the stream and must dispose it.</summary>
public sealed record AvatarRead(Stream Content, string ContentType);
