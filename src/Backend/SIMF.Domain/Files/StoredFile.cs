// Tests: SIMF.Api.Tests/Files/StoredFileTests.cs (invariants),
//        SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download round-trips).
using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Files;

/// <summary>
/// D-568 — the single, unified file record for the whole system: <b>one table,
/// one upload API, one download-by-GUID API</b>. Every uploaded or linked file —
/// avatar, ID document, VIP photo, media-gallery image, speaker photo, speaker
/// presentation, session recording, every logo, news / archive / banner image —
/// is one row here, distinguished only by metadata. It supersedes the seven
/// bespoke filesystem stores and the D-357 <c>Asset</c> store.
///
/// <para><b>Bytes live OUTSIDE the row</b> (D-90): an
/// <see cref="FileSourceType.Upload"/> file stores its bytes on disk under
/// <see cref="StorageKey"/> (encrypted at rest when <see cref="IsEncrypted"/>);
/// an <see cref="FileSourceType.ExternalLink"/> file references
/// <see cref="ExternalUrl"/> and stores no bytes.</para>
///
/// <para><b>No cross-database / cross-table FK</b> (D-157):
/// <see cref="OwnerEntityId"/> is a polymorphic <b>bare Guid</b> (its meaning is
/// fixed by <see cref="OwnerEntityType"/>), resolved on read; the uploader is the
/// inherited <see cref="BaseAuditEntity.CreatedBy"/> (a bare Guid to
/// <c>SimfUser.Id</c> in the Identity DB). The actor's display name is captured
/// in <c>OperationLog</c> at write time, never copied onto this live row.</para>
///
/// <para>Soft-deleted via the inherited <see cref="BaseAuditEntity.IsActive"/> /
/// <see cref="BaseAuditEntity.Deactivate"/>. <see cref="IsDeletable"/> is a
/// separate gate: a legally-retained file (e.g. an ID document under a retention
/// hold) cannot be deleted until <see cref="RetainUntil"/> passes. All
/// server-controlled fields (<see cref="StorageKey"/>, <see cref="IsEncrypted"/>,
/// <see cref="SensitivityTier"/>, owner) are set by the file service from the
/// resolved <c>FileServicePolicy</c> — never from client input.</para>
/// </summary>
public sealed class StoredFile : BaseAuditEntity
{
    /// <summary>The business category — the one dimension that drives access
    /// control, encryption, the upload allow-list, retention and disposal.</summary>
    public FileService Service { get; set; }

    /// <summary>Data-classification tier, derived from <see cref="Service"/> at
    /// write time and persisted so the classification is auditable.</summary>
    public FileSensitivityTier SensitivityTier { get; set; }

    /// <summary>The media kind — drives the per-type upload allow-list.</summary>
    public FileType FileType { get; set; }

    /// <summary>Uploaded bytes vs an external URL.</summary>
    public FileSourceType SourceType { get; set; }

    /// <summary>True when the on-disk bytes are envelope AES-256-GCM encrypted.
    /// Server-set from the <c>FileServicePolicy</c>; never client-downgradable.</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>On-disk blob layout discriminator: <c>0</c> = plaintext / legacy
    /// single-key format; <c>1</c> = envelope format. The read path selects the
    /// decryptor by this value.</summary>
    public byte CipherFormatVersion { get; set; }

    /// <summary>Server-built relative key <c>{Service}/{Id:N}.{ext}</c> under the
    /// file-storage root — set when <see cref="SourceType"/> is
    /// <see cref="FileSourceType.Upload"/>. Never derived from client input.</summary>
    public string? StorageKey { get; set; }

    /// <summary>The external URL — set when <see cref="SourceType"/> is
    /// <see cref="FileSourceType.ExternalLink"/>.</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>The sanitized original file name, for display / download only
    /// (never used to build <see cref="StorageKey"/>).</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>Server-detected MIME type of the stored bytes (Upload only).</summary>
    public string? ContentType { get; set; }

    /// <summary>Size of the plaintext bytes, in bytes (Upload only).</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Hex SHA-256 of the plaintext at ingest — integrity verification
    /// on private download and on the scheduled integrity sweep (Upload only).</summary>
    public string? Sha256 { get; set; }

    /// <summary>Whether deletion is permitted. <c>false</c> blocks delete while a
    /// retention hold is in force; the delete endpoint returns 409.</summary>
    public bool IsDeletable { get; set; } = true;

    /// <summary>End-of-retention timestamp computed from the <see cref="Service"/>
    /// retention policy at write time; drives the secure-erase sweep. Null = indefinite.</summary>
    public DateTimeOffset? RetainUntil { get; set; }

    /// <summary>Set when the bytes / key were crypto-shredded (encrypted) or
    /// securely overwritten (plaintext); distinguishes secure-erased from soft-deleted.</summary>
    public DateTimeOffset? SecureDestroyed { get; set; }

    /// <summary>Which entity family this file belongs to (fixes the meaning of
    /// <see cref="OwnerEntityId"/>). <see cref="FileOwnerEntityType.None"/> when standalone.</summary>
    public FileOwnerEntityType OwnerEntityType { get; set; } = FileOwnerEntityType.None;

    /// <summary>The owning entity's id — a polymorphic <b>bare Guid</b> logical
    /// reference with no DB FK (D-157), resolved on read. For owner-scoped
    /// services (<see cref="FileService.Avatar"/> / <see cref="FileService.IdDocument"/>
    /// / <see cref="FileService.VipPhoto"/>) this is non-null and server-derived
    /// from the authenticated subject; the file service enforces that invariant.</summary>
    public Guid? OwnerEntityId { get; set; }

    /// <summary>Stamp the file as securely destroyed (DEK crypto-shredded or bytes
    /// overwritten). Idempotent.</summary>
    public void MarkSecurelyDestroyed(DateTimeOffset whenUtc)
    {
        SecureDestroyed ??= whenUtc;
        IsActive = false;
    }
}
