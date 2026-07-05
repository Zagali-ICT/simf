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

    /// <summary>D-568 (Wave C S7) — streams <paramref name="content"/> to disk for a
    /// file WITHOUT buffering it whole, computing the SHA-256 of the bytes written
    /// on the fly. <b>Plaintext only</b> — the result is a seekable file for Range
    /// streaming (AES-GCM is not seekable), so this is for <c>EncryptAtRest:false</c>
    /// services (session recordings). Returns the storage key + hash + byte count.</summary>
    Task<StreamWriteResult> WriteStreamAsync(
        FileService service, Guid fileId, string extension, Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>D-568 (Wave C S7) — opens a stored <b>plaintext</b> file as an async,
    /// SEEKABLE read stream (+ its length) so the caller can Range-stream it (HTTP
    /// 206). Returns null when the key is missing or escapes the storage root.
    /// Encrypted files are not seekable — use <see cref="ReadAsync"/> for those.</summary>
    Task<FileReadStream?> OpenReadAsync(
        string storageKey, CancellationToken cancellationToken = default);

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

/// <summary>D-568 (Wave C S7) — the outcome of a streamed (plaintext) write: the
/// storage key, the SHA-256 of the bytes (lowercase hex), and the byte count.</summary>
public sealed record StreamWriteResult(string StorageKey, string Sha256, long SizeBytes);

/// <summary>D-568 (Wave C S7) — a seekable read stream over a stored plaintext file
/// plus its length, for Range streaming. The caller disposes the stream.</summary>
public sealed record FileReadStream(Stream Content, long Length);
