using SIMF.Common.Enums;
using SIMF.Common.Files;

namespace SIMF.Application.Files.Abstractions;

/// <summary>D-568 — the one service behind the centralized file store. Every
/// upload runs the same pipeline (validate → magic-byte allow-list → fail-closed
/// scan → SHA-256 → encrypt-per-policy → store → persist → audit); every download
/// authorizes off the file's own <see cref="FileService"/> (never the URL), so a
/// guessed GUID for a private file is rejected.</summary>
public interface IFileService
{
    /// <summary>Validates, scans, stores and records an uploaded file. Throws an
    /// <c>ApiException</c> (400/409) on a bad size / type / scan.</summary>
    Task<StoredFileResult> UploadAsync(UploadFileCommand command, CancellationToken cancellationToken = default);

    /// <summary>P6 (D-568) — records an EXTERNAL image link (no bytes; the download
    /// endpoint 302-redirects to it). Owner-upsert: replaces the owner's existing
    /// active file of this service with the link and frees any orphaned uploaded
    /// bytes. Used for seeded / admin-supplied logo URLs. Throws 400 on a
    /// non-public / non-https URL or a missing owner where the policy requires one.</summary>
    Task<StoredFileResult> CreateExternalLinkAsync(
        CreateExternalLinkCommand command, CancellationToken cancellationToken = default);

    /// <summary>D-568 (Wave C S7) — streaming upload for large, admin-trusted media
    /// (session recordings) that must not be buffered whole in memory. The bytes are
    /// streamed source→disk with an incremental SHA-256; the malware scan is SKIPPED
    /// (size-capped policy — an admin-only, extension+MIME-validated video up to
    /// 1 GiB, which a byte[] scan would have to buffer whole). Requires an
    /// <c>EncryptAtRest:false</c> service (a seekable plaintext file for Range
    /// streaming); the caller pre-validates the video content-type + extension.</summary>
    Task<StoredFileResult> CreateStreamedAsync(
        FileService service, Guid? ownerEntityId, Stream content,
        string? originalFileName, string contentType, string extension, Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>D-568 (Wave C S7) — opens an active stored file as a seekable read
    /// stream (+ length) for Range streaming (HTTP 206), or null when the id is not
    /// an active file. A RAW open (no per-call authorization): the caller does its own
    /// authz — the recording stream endpoint gates via its StreamToken scheme + a
    /// publish re-check. Only valid for a plaintext (<c>EncryptAtRest:false</c>) file.</summary>
    Task<FileReadStream?> OpenReadStreamAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Authorizes <paramref name="caller"/> against the file's service
    /// policy and returns the bytes (or a redirect for an external link). Throws a
    /// uniform 404 <c>ApiException</c> when the file is missing OR the caller is not
    /// allowed (no exists-but-forbidden oracle for private files).</summary>
    Task<FileDownload> DownloadAsync(Guid id, FileAccessContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a file AND removes the on-disk bytes (P7 — deletion
    /// honesty: a "deleted" file's bytes do not linger on disk). 404 if missing,
    /// 409 if the file is under a retention hold (<c>IsDeletable == false</c>).
    /// Idempotent on an already-deleted file.</summary>
    Task DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>PDPL right-to-erasure (P7): securely destroys the bytes —
    /// crypto-shreds the wrapped DEK for an encrypted file, overwrites the header
    /// for a plaintext one — and marks the row secure-destroyed, <b>bypassing the
    /// retention hold</b> that blocks <see cref="DeleteAsync"/>. Gated by
    /// <c>Files.ForceDelete</c>. 404 if missing; idempotent on an already-destroyed
    /// file.</summary>
    Task ForceDeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>An upload request. The owner *family* is NOT carried here — it is
/// forced from the service's policy in the file service, so a caller cannot
/// over-post a mismatched <see cref="FileOwnerEntityType"/> (P2 — D-568 hardening).
/// <paramref name="OwnerEntityId"/> must be server-derived for owner-scoped
/// services (never trusted from the client); <paramref name="FailClosed"/> is
/// computed by the endpoint (true in Production).</summary>
public sealed record UploadFileCommand(
    FileService Service,
    Guid? OwnerEntityId,
    byte[] Content,
    string? OriginalFileName,
    string ClientContentType,
    Guid ActorUserId,
    bool FailClosed);

/// <summary>P6 (D-568) — request to record an external image link. The owner
/// family is forced from the service policy (like <see cref="UploadFileCommand"/>);
/// only the owner id rides the command.</summary>
public sealed record CreateExternalLinkCommand(
    FileService Service,
    Guid? OwnerEntityId,
    string Url,
    Guid ActorUserId);

/// <summary>The result of a successful upload — the new file's id and its
/// download URL, plus the resolved metadata.</summary>
public sealed record StoredFileResult(
    Guid Id,
    string Url,
    FileService Service,
    FileType FileType,
    bool IsEncrypted,
    long SizeBytes);

/// <summary>What the download endpoint needs to serve a file: either the bytes
/// (Upload) or a redirect target (ExternalLink), plus the metadata that drives the
/// response headers (content-type, attachment-vs-inline, cache policy).</summary>
public sealed record FileDownload(
    bool IsRedirect,
    string? RedirectUrl,
    byte[]? Content,
    string ContentType,
    string? FileName,
    FileService Service,
    FileSensitivityTier Tier,
    FileAccessClass AccessClass,
    FileType FileType);

/// <summary>The caller identity the download endpoint passes in, built from the
/// request principal (so the service stays free of HTTP types and is unit-testable).</summary>
public sealed record FileAccessContext(
    bool IsAuthenticated,
    Guid? UserId,
    IReadOnlySet<string> Permissions)
{
    /// <summary>True when the caller holds the permission code or the admin wildcard.</summary>
    public bool HasPermission(string? code) =>
        code is not null && (Permissions.Contains(code) || Permissions.Contains("*"));

    /// <summary>An anonymous caller (no principal).</summary>
    public static FileAccessContext Anonymous { get; } =
        new(false, null, new HashSet<string>());
}
