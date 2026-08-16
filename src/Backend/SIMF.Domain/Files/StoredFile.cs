// Tests: SIMF.Api.Tests/Files/StoredFileTests.cs (invariants),
//        SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download round-trips).
using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Files;

/// <summary>The one file record for the whole system: avatars, ID documents, photos,
/// presentations, recordings, logos and banners are all rows here, told apart only by
/// their metadata. There is no second file store. The bytes are not in the row, and
/// every server-controlled field comes from the file's resolved <c>FileServicePolicy</c>,
/// never from client input — read that policy before changing one.</summary>
public sealed class StoredFile : BaseAuditEntity
{
    /// <summary>The single dimension deciding access control, encryption at rest, the
    /// upload allow-list, retention and disposal.</summary>
    public FileService Service { get; set; }

    /// <summary>Derived from <see cref="Service"/> and persisted, so the tier a file was
    /// stored under survives a policy change.</summary>
    public FileSensitivityTier SensitivityTier { get; set; }

    /// <summary>Checked against the per-service upload allow-list.</summary>
    public FileType FileType { get; set; }

    public FileSourceType SourceType { get; set; }

    /// <summary>True when the bytes on disk are AES-256-GCM envelope encrypted.</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>On-disk blob layout: 0 plaintext, 1 the current envelope format.</summary>
    public byte CipherFormatVersion { get; set; }

    // Exactly one is set, per SourceType. The server builds StorageKey as
    // {Service}/{Id:N}.{ext} beneath the storage root, taking nothing from the client,
    // so an uploaded name cannot steer a write out of the root.
    public string? StorageKey { get; set; }
    public string? ExternalUrl { get; set; }

    /// <summary>The sanitized arrival name, for display and the download filename only.</summary>
    public string? OriginalFileName { get; set; }

    // Upload-only. ContentType is server-detected from the bytes, not the client's
    // declaration. SizeBytes and the hex Sha256 describe the PLAINTEXT, so an encrypted
    // blob is larger and hashes differently; the hash is re-checked only on a private
    // download of a Confidential-or-above file.
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }

    // IsDeletable false is a legal hold, and the delete endpoint answers 409. RetainUntil
    // is aspirational today: it is computed from the service's FileServicePolicy.Retention,
    // and no policy in the registry sets one, so the column stays null on every row. No
    // sweep reads it either — disposal is always deliberate, through the force-delete path.
    // SecureDestroyedAt means the bytes are gone for good (crypto-shredded or overwritten),
    // stamped only by the right-to-erasure path.
    public bool IsDeletable { get; set; } = true;
    public DateTime? RetainUntil { get; set; }
    public DateTime? SecureDestroyedAt { get; set; }

    // OwnerEntityId is polymorphic: OwnerEntityType fixes which table it points into. For
    // the owner-scoped services (Avatar, IdDocument, VipPhoto) it is mandatory and the
    // server derives it from the authenticated subject, never from the request.
    public FileOwnerEntityType OwnerEntityType { get; set; } = FileOwnerEntityType.None;
    public Guid? OwnerEntityId { get; set; }

    public void MarkSecurelyDestroyed(DateTime whenUtc)
    {
        SecureDestroyedAt ??= whenUtc;
        IsActive = false;
    }
}
