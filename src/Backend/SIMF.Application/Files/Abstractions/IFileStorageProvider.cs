using SIMF.Common.Enums;

namespace SIMF.Application.Files.Abstractions;

/// <summary>D-568 — the single storage seam for the centralized file store.
/// Filesystem-backed today; a future S3 / Azure-Blob provider is a DI swap with
/// no caller change. The on-disk key is always server-built from the
/// <see cref="FileService"/> + file id (never client input), so path traversal is
/// impossible by construction, and every read re-validates the key against the
/// storage root.</summary>
public interface IFileStorageProvider
{
    /// <summary>Writes <paramref name="content"/> for a file, encrypting at rest
    /// when <paramref name="encrypt"/> is true. The storage key is derived from
    /// <paramref name="service"/> + <paramref name="fileId"/> + a sanitized
    /// <paramref name="extension"/>. Returns the key to persist on the row and the
    /// cipher format version used (<c>0</c> when stored in plaintext).</summary>
    Task<FileWriteResult> WriteAsync(
        FileService service, Guid fileId, string extension, byte[] content, bool encrypt,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the bytes for a stored key, decrypting when
    /// <paramref name="encrypted"/> is true. Returns null when the key is missing
    /// or escapes the storage root.</summary>
    Task<byte[]?> ReadAsync(
        string storageKey, bool encrypted, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file for a stored key (idempotent).</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Securely destroys the file for a stored key: for an encrypted file
    /// this crypto-shreds the wrapped DEK in the header (rendering the ciphertext
    /// unrecoverable) before unlinking; for a plaintext file it overwrites the
    /// header region then unlinks. Idempotent.</summary>
    Task SecureEraseAsync(string storageKey, CancellationToken cancellationToken = default);
}

/// <summary>The key to persist on the <c>StoredFile</c> row plus the cipher
/// format version used (<c>0</c> = plaintext).</summary>
public sealed record FileWriteResult(string StorageKey, byte CipherFormatVersion);
