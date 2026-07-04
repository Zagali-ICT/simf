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

    /// <summary>Authorizes <paramref name="caller"/> against the file's service
    /// policy and returns the bytes (or a redirect for an external link). Throws a
    /// uniform 404 <c>ApiException</c> when the file is missing OR the caller is not
    /// allowed (no exists-but-forbidden oracle for private files).</summary>
    Task<FileDownload> DownloadAsync(Guid id, FileAccessContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a file. 404 if missing, 409 if the file is under a
    /// retention hold (<c>IsDeletable == false</c>). Idempotent on an already-deleted file.</summary>
    Task DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
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
