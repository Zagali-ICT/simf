namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Encrypted-at-rest storage for the user's ID-document image (decisions
/// D-046 b, P8 — D-049; renamed from <c>IVisitorIdStorage</c>). The
/// implementation persists the bytes on disk; the cipher is AES-GCM with
/// a per-installation 32-byte key supplied through configuration. The
/// plaintext bytes never sit on disk; the disk file is opaque without
/// the key.
/// </summary>
public interface IUserIdDocumentStorage
{
    /// <summary>Saves the supplied bytes encrypted; returns the relative
    /// path to persist on the profile row.</summary>
    Task<string> SaveAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Reads + decrypts the file at the relative path. Returns
    /// null when the file is missing or the relative path is suspicious.</summary>
    Task<UserIdDocumentRead?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Removes the file at the relative path (idempotent).</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

/// <summary>Decrypted bytes + the original content type recovered from the file.</summary>
public sealed record UserIdDocumentRead(byte[] Content, string ContentType);
