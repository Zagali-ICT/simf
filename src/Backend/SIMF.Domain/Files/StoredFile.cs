// Tests: SIMF.Api.Tests/Files/StoredFileTests.cs (invariants),
//        SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download round-trips).
using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Files;

/// <summary>
/// The one file record for the whole system: one table, one upload API, one
/// download-by-GUID API. Avatars, ID documents, speaker photos, presentations,
/// session recordings, logos and banners are all rows here, told apart only by
/// their metadata. There is no second file store.
///
/// <para><b>The bytes are not in the row</b>: they sit on disk under
/// <see cref="StorageKey"/>, or at somebody else's <see cref="ExternalUrl"/>.</para>
///
/// <para>The uploader is the inherited <see cref="BaseAuditEntity.CreatedBy"/>, a
/// bare Guid rather than a navigation because that user lives in the Identity
/// database and no foreign key crosses the two. Their display name is captured in
/// <c>OperationLog</c> at write time, never copied onto this live row.</para>
///
/// <para>Every server-controlled field, <see cref="StorageKey"/> and
/// <see cref="IsEncrypted"/> and <see cref="SensitivityTier"/> and the owner among
/// them, comes from the file's resolved <c>FileServicePolicy</c> and never from
/// client input. Read that policy before changing one.</para>
/// </summary>
public sealed class StoredFile : BaseAuditEntity
{
    /// <summary>The single dimension that decides access control, encryption at
    /// rest, the upload allow-list, retention and disposal.</summary>
    public FileService Service { get; set; }

    /// <summary>Derived from <see cref="Service"/> at write time, not chosen, and
    /// persisted so the tier a file was stored under survives a policy change.</summary>
    public FileSensitivityTier SensitivityTier { get; set; }

    /// <summary>Checked against the per-service upload allow-list.</summary>
    public FileType FileType { get; set; }

    public FileSourceType SourceType { get; set; }

    /// <summary>True when the bytes on disk are AES-256-GCM envelope encrypted.
    /// Server-set from the service policy; a client cannot downgrade it.</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>Which on-disk blob layout was written: 0 plaintext, 1 the current
    /// envelope format. Persisted so a later format change can still read blobs
    /// written under the old one.</summary>
    public byte CipherFormatVersion { get; set; }

    // Exactly one of these is set, according to SourceType; switching a row
    // between the modes clears the other. StorageKey is built by the server as
    // {Service}/{Id:N}.{ext} beneath the storage root and takes nothing from the
    // client, so an uploaded name cannot steer a write out of the root.
    public string? StorageKey { get; set; }
    public string? ExternalUrl { get; set; }

    /// <summary>The sanitized name the file arrived with, for display and the
    /// download filename only. It never feeds <see cref="StorageKey"/>.</summary>
    public string? OriginalFileName { get; set; }

    // Upload-only, all null for an external link. ContentType is server-detected
    // from the bytes, not the client's declaration; SizeBytes and the hex Sha256
    // describe the PLAINTEXT, so an encrypted blob on disk is larger and hashes
    // differently. The hash is re-checked on private download and by the sweep.
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }

    // Retention and disposal. IsDeletable false holds the file against deletion
    // while a legal hold is in force, and the delete endpoint answers 409.
    // RetainUntil comes from the service's retention period at write time and
    // drives the secure-erase sweep; null means keep indefinitely, not expired.
    // SecureDestroyed marks the bytes going for good, by crypto-shredding the key
    // or overwriting the plaintext, and is what separates a securely erased row
    // from a merely soft-deleted one.
    public bool IsDeletable { get; set; } = true;
    public DateTime? RetainUntil { get; set; }
    public DateTime? SecureDestroyed { get; set; }

    // OwnerEntityId is a bare Guid with no foreign key because it is polymorphic:
    // OwnerEntityType fixes which table it points into, and one column cannot be
    // constrained to a dozen of them. None means the file is standalone and the id
    // is null. For the owner-scoped services (Avatar, IdDocument, VipPhoto) the id
    // is mandatory and the server derives it from the authenticated subject rather
    // than accepting it from the request; the file service enforces that.
    public FileOwnerEntityType OwnerEntityType { get; set; } = FileOwnerEntityType.None;
    public Guid? OwnerEntityId { get; set; }

    /// <summary>Stamps the moment the bytes were destroyed and soft-deletes the
    /// row with it. The timestamp is written once, so a repeated erase keeps the
    /// original moment rather than moving it forward.</summary>
    public void MarkSecurelyDestroyed(DateTime whenUtc)
    {
        SecureDestroyed ??= whenUtc;
        IsActive = false;
    }
}
