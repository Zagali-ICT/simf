// Tests: SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download/delete),
//        SIMF.Api.Tests/Files/FileAuthorizationTests.cs (per-service authz + IDOR),
//        SIMF.Api.Tests/IdentitySeederTests.cs (ContentExistsAsync — both the
//        missing-bytes and the missing-row shapes, via the demo-image repair).
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Files;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Files;

/// <summary>The one service behind the centralized file store. Named
/// <c>StoredFileService</c> (not <c>FileService</c>) to avoid colliding with the
/// <see cref="FileService"/> business-category enum. Upload runs one pipeline for
/// every file; download authorizes off the stored file's own service policy.</summary>
internal sealed class StoredFileService(
    SimfAppDbContext dbContext,
    IFileStorageProvider storage,
    IUploadScanner scanner,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<StoredFileService> logger) : IFileService
{
    public async Task<StoredFileResult> UploadAsync(
        UploadFileCommand command, CancellationToken cancellationToken = default)
    {
        var policy = FileServicePolicies.Resolve(command.Service);

        if (command.Content is null || command.Content.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "No file was uploaded.", "لم يتم رفع أي ملف.");
        }

        var detected = DetectUpload(command.Content, command.OriginalFileName);
        if (!policy.AllowedTypes.Contains(detected.Type))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"A {detected.Type} file is not allowed for {command.Service}.",
                "نوع الملف غير مسموح به لهذا القسم.");
        }
        if (command.Content.LongLength > MaxBytesFor(detected.Type))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"The file exceeds the {MaxBytesFor(detected.Type) / (1024 * 1024)} MB limit.",
                "حجم الملف يتجاوز الحد المسموح به.");
        }
        if (policy.OwnerRequired && (command.OwnerEntityId is null || command.OwnerEntityId == Guid.Empty))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "This file category requires an owner.", "هذا النوع من الملفات يتطلب مالكًا.");
        }

        // Scan before storing; fail-closed in Production.
        await scanner.EnsureCleanFailClosedAsync(
            command.Content, command.OriginalFileName ?? "upload", command.FailClosed, cancellationToken);

        var sha256 = Convert.ToHexString(SHA256.HashData(command.Content)).ToLowerInvariant();
        var fileId = Guid.NewGuid();
        var write = await storage.WriteAsync(
            command.Service, fileId, detected.Extension, command.Content, policy.EncryptAtRest, cancellationToken);

        var now = timeProvider.SimfNow();
        var file = new StoredFile
        {
            Id = fileId,
            Service = command.Service,
            SensitivityTier = policy.Tier,
            FileType = detected.Type,
            SourceType = FileSourceType.Upload,
            IsEncrypted = policy.EncryptAtRest,
            CipherFormatVersion = write.CipherFormatVersion,
            // The KEK that actually wrapped this file's data key, carried out of
            // the write rather than re-read from configuration, so a rotation can
            // be inventoried and resumed from SQL.
            KekVersion = write.KekVersion,
            StorageKey = write.StorageKey,
            OriginalFileName = SanitizeFileName(command.OriginalFileName),
            ContentType = detected.ContentType,
            SizeBytes = command.Content.LongLength,
            Sha256 = sha256,
            IsDeletable = policy.DeletableDefault,
            RetainUntil = policy.Retention is { } retention ? now.Add(retention) : null,
            // The owner FAMILY is authoritative from the
            // policy, never the client, so a caller can't over-post a mismatched
            // owner type. Only the owner id rides the request (server-derived for
            // owner-scoped services by the caller).
            OwnerEntityType = policy.OwnerEntityType,
            OwnerEntityId = command.OwnerEntityId,
            CreatedBy = command.ActorUserId,
            CreatedAt = now,
            IsActive = true,
        };
        dbContext.StoredFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        await OwnerPointerSync.PointAtAsync(
            dbContext, command.Service, command.OwnerEntityId, fileId, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileUploaded,
            command.ActorUserId,
            $"id={fileId}; service={command.Service}; type={detected.Type}; "
                + $"bytes={command.Content.LongLength}; encrypted={policy.EncryptAtRest}",
            cancellationToken);

        logger.LogInformation(
            "File {Id} uploaded (service={Service}, type={Type}, {Bytes} bytes, encrypted={Encrypted}).",
            fileId, command.Service, detected.Type, command.Content.LongLength, policy.EncryptAtRest);

        return new StoredFileResult(
            fileId, $"/api/v1/files/{fileId}", command.Service, detected.Type,
            policy.EncryptAtRest, command.Content.LongLength);
    }

    public async Task<StoredFileResult> CreateStreamedAsync(
        FileService service, Guid? ownerEntityId, Stream content,
        string? originalFileName, string contentType, string extension, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var policy = FileServicePolicies.Resolve(service);
        // Streamed uploads are for large, admin-trusted plaintext media (recordings) —
        // the stored file must stay seekable for Range streaming, and AES-GCM is not
        // seekable, so this path is only valid for an EncryptAtRest:false service.
        if (policy.EncryptAtRest)
        {
            throw new InvalidOperationException(
                $"CreateStreamedAsync requires an EncryptAtRest:false service; {service} encrypts at rest.");
        }
        if (policy.OwnerRequired && (ownerEntityId is null || ownerEntityId == Guid.Empty))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "This file category requires an owner.", "هذا النوع من الملفات يتطلب مالكًا.");
        }

        // The malware scan is SKIPPED for streamed uploads: the
        // caller is an admin-only, extension+MIME-validated video up to 1 GiB, and
        // buffering it whole for a byte[] scan would defeat the streaming pipeline.
        var fileId = Guid.NewGuid();
        var write = await storage.WriteStreamAsync(service, fileId, extension, content, cancellationToken);

        var now = timeProvider.SimfNow();
        var file = new StoredFile
        {
            Id = fileId,
            Service = service,
            SensitivityTier = policy.Tier,
            FileType = FileType.Video,
            SourceType = FileSourceType.Upload,
            IsEncrypted = false,
            CipherFormatVersion = 0,
            // Streamed recordings are written plaintext (AES-GCM is not seekable),
            // so no KEK wrapped anything here.
            KekVersion = null,
            StorageKey = write.StorageKey,
            OriginalFileName = SanitizeFileName(originalFileName),
            ContentType = contentType,
            SizeBytes = write.SizeBytes,
            Sha256 = write.Sha256,
            IsDeletable = policy.DeletableDefault,
            RetainUntil = policy.Retention is { } retention ? now.Add(retention) : null,
            OwnerEntityType = policy.OwnerEntityType,
            OwnerEntityId = ownerEntityId,
            CreatedBy = actorUserId,
            CreatedAt = now,
            IsActive = true,
        };
        dbContext.StoredFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        await OwnerPointerSync.PointAtAsync(
            dbContext, service, ownerEntityId, fileId, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileUploaded,
            actorUserId,
            $"id={fileId}; service={service}; type=Video; bytes={write.SizeBytes}; encrypted=false; streamed",
            cancellationToken);

        logger.LogInformation(
            "File {Id} streamed (service={Service}, {Bytes} bytes, plaintext).", fileId, service, write.SizeBytes);

        return new StoredFileResult(
            fileId, $"/api/v1/files/{fileId}", service, FileType.Video, false, write.SizeBytes);
    }

    public async Task<FileReadStream?> OpenReadStreamAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var storageKey = await dbContext.StoredFiles
            .AsNoTracking()
            .Where(f => f.Id == id && f.IsActive && f.StorageKey != null)
            .Select(f => f.StorageKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (storageKey is null) { return null; }
        return await storage.OpenReadAsync(storageKey, cancellationToken);
    }

    public async Task<StoredFileResult> CreateExternalLinkAsync(
        CreateExternalLinkCommand command, CancellationToken cancellationToken = default)
    {
        var policy = FileServicePolicies.Resolve(command.Service);
        var url = ValidateExternalLink(command.Url, policy);
        if (policy.OwnerRequired && (command.OwnerEntityId is null || command.OwnerEntityId == Guid.Empty))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "This file category requires an owner.", "هذا النوع من الملفات يتطلب مالكًا.");
        }

        var now = timeProvider.SimfNow();

        // Owner-upsert (ported from AssetService.SetExternalLinkAsync): replace the
        // owner's existing active file of this service with the link, freeing any
        // orphaned uploaded bytes. A standalone (no owner) link always inserts.
        StoredFile? existing = null;
        if (command.OwnerEntityId is { } ownerId)
        {
            existing = await dbContext.StoredFiles.FirstOrDefaultAsync(
                f => f.Service == command.Service
                    && f.OwnerEntityType == policy.OwnerEntityType
                    && f.OwnerEntityId == ownerId
                    && f.IsActive,
                cancellationToken);
        }

        var previousUploadKey =
            existing is { SourceType: FileSourceType.Upload, StorageKey: { Length: > 0 } key } ? key : null;

        StoredFile file;
        if (existing is null)
        {
            file = new StoredFile
            {
                Id = Guid.NewGuid(),
                Service = command.Service,
                SensitivityTier = policy.Tier,
                OwnerEntityType = policy.OwnerEntityType,
                OwnerEntityId = command.OwnerEntityId,
                CreatedBy = command.ActorUserId,
                CreatedAt = now,
                IsActive = true,
            };
            dbContext.StoredFiles.Add(file);
        }
        else
        {
            file = existing;
            file.UpdatedBy = command.ActorUserId;
            file.UpdatedAt = now;
        }

        file.SourceType = FileSourceType.ExternalLink;
        file.ExternalUrl = url;
        // Derived from the policy, not assumed. A hardcoded Image mistyped a video
        // link, and the download endpoint serves inline only when the file is both
        // public-tier AND typed Image — so a mistyped video would have been offered
        // as an inline document rather than a stream.
        file.FileType = policy.AllowedTypes.Contains(FileType.Image)
            ? FileType.Image
            : policy.AllowedTypes.First();
        file.StorageKey = null;
        file.ContentType = null;
        file.SizeBytes = null;
        file.Sha256 = null;
        file.OriginalFileName = null;
        file.IsEncrypted = false;
        file.CipherFormatVersion = 0;
        // This row is being converted from an upload to an external link, so the
        // bytes it named are gone and the KEK stamp left behind would name a key
        // this row no longer has anything to do with - and would be counted by a
        // rotation inventory that has nothing to re-wrap.
        file.KekVersion = null;
        file.IsDeletable = policy.DeletableDefault;

        await dbContext.SaveChangesAsync(cancellationToken);

        await OwnerPointerSync.PointAtAsync(
            dbContext, command.Service, command.OwnerEntityId, file.Id, cancellationToken);

        // Free the swapped-out upload's bytes only after the row is safely persisted.
        if (previousUploadKey is not null)
        {
            await storage.DeleteAsync(previousUploadKey, cancellationToken);
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileLinked,
            command.ActorUserId,
            $"id={file.Id}; service={command.Service}; url={url}",
            cancellationToken);

        return new StoredFileResult(
            file.Id, $"/api/v1/files/{file.Id}", command.Service, file.FileType, IsEncrypted: false, SizeBytes: 0);
    }

    public async Task<FileDownload> DownloadAsync(
        Guid id, FileAccessContext caller, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, cancellationToken);
        if (file is null) { throw NotFound(); }

        var policy = FileServicePolicies.Resolve(file.Service);
        if (!IsAuthorized(policy, file, caller))
        {
            await auditLog.WriteFailureAsync(
                AuditEvents.FileAccessDenied,
                caller.UserId,
                detail: $"id={id}; service={file.Service}",
                cancellationToken: cancellationToken);
            // Uniform 404 — no exists-but-forbidden oracle for a private file.
            throw NotFound();
        }

        if (file.SourceType == FileSourceType.ExternalLink)
        {
            return new FileDownload(
                IsRedirect: true, RedirectUrl: file.ExternalUrl, Content: null,
                ContentType: file.ContentType ?? "text/plain", FileName: null,
                file.Service, policy.Tier, policy.Access, file.FileType);
        }

        if (string.IsNullOrEmpty(file.StorageKey)) { throw NotFound(); }
        var bytes = await storage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        if (bytes is null) { throw NotFound(); }

        if (!await IntegrityVerifiedAsync(file, policy.Tier, bytes, caller.UserId, cancellationToken))
        {
            throw NotFound();
        }

        // Per-row audit only for non-public reads (public reads would flood the log).
        if (policy.Access != FileAccessClass.Public)
        {
            await auditLog.WriteSuccessAsync(
                AuditEvents.FileDownloaded,
                caller.UserId,
                $"id={id}; service={file.Service}",
                cancellationToken);
        }

        return new FileDownload(
            IsRedirect: false, RedirectUrl: null, Content: bytes,
            ContentType: file.ContentType ?? "application/octet-stream",
            FileName: file.OriginalFileName,
            file.Service, policy.Tier, policy.Access, file.FileType);
    }

    public async Task<StoredFileContent?> ReadContentAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, cancellationToken);
        return file is null ? null : await ReadVerifiedAsync(file, cancellationToken);
    }

    public async Task<StoredFileContent?> ReadOwnerContentAsync(
        FileService service, Guid ownerEntityId, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles.AsNoTracking()
            .Where(f => f.Service == service && f.OwnerEntityId == ownerEntityId && f.IsActive)
            // Newest active wins; Id is a deterministic tiebreak for the rare
            // same-tick case (a fake TimeProvider, or a brief replace window).
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return file is null ? null : await ReadVerifiedAsync(file, cancellationToken);
    }

    public async Task<bool> RestoreBytesAsync(
        Guid id, byte[] content, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (content is null || content.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "No file content was supplied.", "لم يتم تزويد محتوى الملف.");
        }

        // Tracked, not AsNoTracking: a restore re-seals the bytes and can move the
        // row's cipher stamps, which have to be saved with it.
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, cancellationToken);
        if (file is null
            || file.SourceType != FileSourceType.Upload
            || string.IsNullOrEmpty(file.StorageKey))
        {
            return false;
        }

        // The provider rebuilds the SAME {service}/{id:N}{ext} key the row records,
        // so writing by (service, id, extension) cannot land the bytes anywhere the
        // row does not already point at.
        var write = await storage.WriteAsync(
            file.Service, file.Id, Path.GetExtension(file.StorageKey), content,
            file.IsEncrypted, cancellationToken);

        // The restored bytes are sealed under whatever KEK is active NOW, which
        // during a rotation window is not the one this row was stamped with. Left
        // stale, the row would name a key the blob is no longer wrapped under, and
        // a rotation inventory would read this file as already re-wrapped when it
        // is not - the one failure this column exists to prevent.
        if (file.CipherFormatVersion != write.CipherFormatVersion
            || file.KekVersion != write.KekVersion)
        {
            file.CipherFormatVersion = write.CipherFormatVersion;
            file.KekVersion = write.KekVersion;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileUploaded,
            actorUserId,
            $"id={file.Id}; service={file.Service}; bytes={content.LongLength}; "
                + $"encrypted={file.IsEncrypted}; restored",
            cancellationToken);

        return true;
    }

    public async Task<bool> ContentExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles.AsNoTracking()
            .Where(f => f.Id == id && f.IsActive)
            .Select(f => new { f.SourceType, f.StorageKey })
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null) { return false; }

        // An external link owns no bytes here, so the row IS the content.
        if (file.SourceType == FileSourceType.ExternalLink) { return true; }

        return file.StorageKey is { Length: > 0 } key
            && await storage.ExistsAsync(key, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw NotFound();
        if (!file.IsActive) { return; }
        if (!file.IsDeletable)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 409,
                "This file is under a retention hold and cannot be deleted.",
                "هذا الملف خاضع لفترة احتفاظ ولا يمكن حذفه.");
        }

        var now = timeProvider.SimfNow();
        file.Deactivate();
        file.DeletedAt = now;
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // The owning row must not keep pointing at a file that is no longer
        // there. This path is reachable without the asset service — a direct
        // DELETE /files/{id} — so the pointer is maintained here rather than
        // only where assets are managed.
        await OwnerPointerSync.ClearIfPointingAtAsync(
            dbContext, file.Service, file.OwnerEntityId, file.Id, cancellationToken);

        // Deletion honesty: a soft-deleted file's bytes must
        // not linger on disk. Unlink the stored blob (Upload only; an ExternalLink
        // holds no bytes). Best-effort after the row commit — the row is the source
        // of truth (IsActive=false already makes the file un-downloadable).
        if (file.SourceType == FileSourceType.Upload && !string.IsNullOrEmpty(file.StorageKey))
        {
            await storage.DeleteAsync(file.StorageKey, cancellationToken);
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileDeleted,
            actorUserId,
            $"id={id}; service={file.Service}",
            cancellationToken);
    }

    public async Task ForceDeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw NotFound();
        if (file.SecureDestroyedAt is not null) { return; } // idempotent

        // PDPL right-to-erasure. Securely destroy the bytes (crypto-shred the
        // wrapped DEK for an encrypted file, overwrite the header for a plaintext
        // one), bypassing the retention hold that blocks the ordinary delete.
        if (file.SourceType == FileSourceType.Upload && !string.IsNullOrEmpty(file.StorageKey))
        {
            await storage.SecureEraseAsync(file.StorageKey, cancellationToken);
        }

        var now = timeProvider.SimfNow();
        file.MarkSecurelyDestroyed(now);
        file.DeletedAt ??= now;
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Erasure has to reach the pointer too, or the owning row would still
        // name a file whose bytes are gone.
        await OwnerPointerSync.ClearIfPointingAtAsync(
            dbContext, file.Service, file.OwnerEntityId, file.Id, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.FileSecurelyDestroyed,
            actorUserId,
            $"id={id}; service={file.Service}; force-delete",
            cancellationToken);
    }

    /// <summary>The read half shared by <see cref="ReadContentAsync"/> and
    /// <see cref="ReadOwnerContentAsync"/>: decrypt per the row, then apply the same
    /// fail-closed integrity re-check the download route applies. No policy check and
    /// no success audit — those callers authorize and audit themselves.</summary>
    private async Task<StoredFileContent?> ReadVerifiedAsync(
        StoredFile file, CancellationToken cancellationToken)
    {
        // An external link owns no bytes here; the caller serves the URL itself.
        if (file.SourceType == FileSourceType.ExternalLink) { return null; }
        if (string.IsNullOrEmpty(file.StorageKey)) { return null; }

        var bytes = await storage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        if (bytes is null) { return null; }

        var tier = FileServicePolicies.Resolve(file.Service).Tier;
        if (!await IntegrityVerifiedAsync(file, tier, bytes, actorUserId: null, cancellationToken))
        {
            return null;
        }
        return new StoredFileContent(bytes, file.ContentType);
    }

    /// <summary>Integrity verification on a private file (SAMA H-29/30). FAIL
    /// CLOSED: a stored-hash mismatch means the bytes on disk were tampered with (or
    /// a decrypt returned garbage) — audit it and refuse to serve, never hand the
    /// caller unverified bytes. Only Confidential+ tiers carry the per-read hash
    /// check (public images are not hashed on read).</summary>
    private async Task<bool> IntegrityVerifiedAsync(
        StoredFile file, FileSensitivityTier tier, byte[] bytes, Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        if (tier < FileSensitivityTier.Confidential || string.IsNullOrEmpty(file.Sha256))
        {
            return true;
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        logger.LogError(
            "Integrity mismatch on file {Id} (service={Service}) — refusing to serve tampered bytes.",
            file.Id, file.Service);
        await auditLog.WriteFailureAsync(
            AuditEvents.FileIntegrityFailed,
            actorUserId,
            detail: $"id={file.Id}; service={file.Service}; expected={file.Sha256}; actual={actual}",
            cancellationToken: cancellationToken);
        return false;
    }

    private static bool IsAuthorized(FileServicePolicy policy, StoredFile file, FileAccessContext caller) =>
        policy.Access switch
        {
            FileAccessClass.Public => true,
            FileAccessClass.Authenticated => caller.IsAuthenticated,
            FileAccessClass.Admin => caller.HasPermission(policy.AdminPermission),
            // Owner-or-admin: a null owner is a HARD deny, never a silent fall-through.
            FileAccessClass.OwnerOrAdmin => file.OwnerEntityId is { } owner
                && (caller.UserId == owner || caller.HasPermission(policy.AdminPermission)),
            _ => false,
        };

    private static ApiException NotFound() =>
        new(ErrorCodes.NotFound, 404, "The file was not found.", "لم يتم العثور على الملف.");

    private static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) { return null; }
        var leaf = Path.GetFileName(name.Trim());
        if (string.IsNullOrEmpty(leaf)) { return null; }
        return leaf.Length > 260 ? leaf[..260] : leaf;
    }

    private static long MaxBytesFor(FileType type) => type switch
    {
        FileType.Image => 10L * 1024 * 1024,
        FileType.Pdf => 25L * 1024 * 1024,
        FileType.Document => 25L * 1024 * 1024,
        FileType.Spreadsheet => 5L * 1024 * 1024,
        FileType.Video => 1024L * 1024 * 1024,
        _ => 10L * 1024 * 1024,
    };

    /// <summary>Determines the real <see cref="FileType"/> from the magic bytes
    /// (never trusting the client content-type), and returns the canonical MIME +
    /// extension. Throws 400 when the bytes are not a recognized allowed shape.</summary>
    private static (FileType Type, string ContentType, string Extension) DetectUpload(
        byte[] content, string? originalFileName)
    {
        if (ImageUploadValidation.MagicBytesMatch(content, "image/png"))
        {
            return (FileType.Image, "image/png", ".png");
        }
        if (ImageUploadValidation.MagicBytesMatch(content, "image/jpeg"))
        {
            return (FileType.Image, "image/jpeg", ".jpg");
        }
        if (ImageUploadValidation.MagicBytesMatch(content, "image/webp"))
        {
            return (FileType.Image, "image/webp", ".webp");
        }
        if (StartsWith(content, "%PDF"u8))
        {
            return (FileType.Pdf, "application/pdf", ".pdf");
        }
        if (content.Length >= 8 && StartsWith(content.AsSpan(4), "ftyp"u8))
        {
            return (FileType.Video, "video/mp4", ExtensionFrom(originalFileName, ".mp4"));
        }
        if (StartsWith(content, [0x1A, 0x45, 0xDF, 0xA3]))
        {
            return (FileType.Video, "video/webm", ".webm");
        }
        if (StartsWith(content, [0x50, 0x4B, 0x03, 0x04]))
        {
            // A ZIP container is an OOXML Office document.
            // The stored MIME is the CANONICAL OOXML type resolved from the
            // declared extension; the client content-type is NEVER echoed (a ZIP
            // could be anything, and trusting the client MIME lets a caller
            // mislabel the stored bytes). An unrecognized OOXML extension is
            // rejected rather than stored as an opaque blob.
            var ext = ExtensionFrom(originalFileName, string.Empty);
            return ext switch
            {
                ".docx" => (FileType.Document,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"),
                ".pptx" => (FileType.Document,
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx"),
                ".xlsx" => (FileType.Spreadsheet,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
                _ => throw new ApiException(ErrorCodes.ValidationFailed, 400,
                    "Only .docx, .pptx and .xlsx Office documents are accepted for this upload.",
                    "لا يُقبل في هذا الرفع سوى مستندات Office بامتداد ‎.docx‎ أو ‎.pptx‎ أو ‎.xlsx‎."),
            };
        }

        throw new ApiException(ErrorCodes.ValidationFailed, 400,
            "The file type could not be recognized or is not allowed.",
            "تعذّر التعرّف على نوع الملف أو أنه غير مسموح به.");
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);

    // External links are 302-served to anonymous visitors, so the
    // target must be a real, public https host. Ported from AssetService.ValidateLink:
    // require https (no cleartext) and reject literal IPs / localhost / internal TLDs
    // so the trusted SIMF domain can't become an open redirect to an internal service.
    //
    // <para>The rule is layered per policy rather than applied uniformly, because
    // the two kinds of external link are consumed differently. An image link is
    // never read by the client: the download endpoint 302s and the browser or Dio
    // follows it, so any public https URL will do — including the extensionless
    // placeholder URLs the demo seeds use. A VIDEO link rides the wire verbatim and
    // BOTH clients classify it by inspecting the string (YouTube id, else a
    // .mp4/.m3u8 suffix), so one that fails that test is not a video they can play,
    // and storing it would produce a silent empty hero or a player error rather
    // than a 400 the admin can act on.</para>
    private static string ValidateExternalLink(string url, FileServicePolicy policy)
    {
        // A private file must never become a pointer at somebody else's server.
        // Nothing else stopped POST /files/link naming a Secret or Confidential
        // service (an ID document, an avatar), which would have created an
        // unencrypted, unscanned row that bypasses the whole ingest pipeline while
        // still being served under this system's name.
        if (policy.Tier != FileSensitivityTier.Public || policy.Access != FileAccessClass.Public)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Only public files may be recorded as an external link.",
                "لا يمكن تسجيل رابط خارجي إلا للملفات العامة.");
        }

        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 1024
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !IsPublicHost(uri))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Provide a valid public https URL (max 1024 characters).",
                "يرجى إدخال رابط https عام صحيح لا يتجاوز 1024 حرفاً.");
        }

        // A video link must additionally survive the clients' own classifier,
        // which is the same rule LiveStreamUrlPolicy states for a live feed.
        // Deliberately NOT applied to image services: it accepts only a YouTube
        // id or a .m3u8/.mp4 suffix, so it would reject every CDN logo, every
        // seeded placeholder, and the whole "External link" tab.
        if (policy.AllowedTypes.Contains(FileType.Video)
            && !policy.AllowedTypes.Contains(FileType.Image)
            && !LiveStreamUrlPolicy.IsAllowed(trimmed))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Provide a YouTube video link or a direct .mp4 / .m3u8 stream URL.",
                "يرجى إدخال رابط فيديو يوتيوب أو رابط بث مباشر بصيغة mp4 أو m3u8.");
        }

        return trimmed;
    }

    private static bool IsPublicHost(Uri uri)
    {
        var host = uri.Host;
        if (string.IsNullOrEmpty(host)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !System.Net.IPAddress.TryParse(host, out _);
    }

    private static string ExtensionFrom(string? originalFileName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(originalFileName)) { return fallback; }
        var ext = Path.GetExtension(originalFileName);
        if (ext.Length is < 2 or > 16) { return fallback; }
        for (var i = 1; i < ext.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(ext[i])) { return fallback; }
        }
        return ext.ToLowerInvariant();
    }
}
