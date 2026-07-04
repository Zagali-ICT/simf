// Tests: SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download/delete),
//        SIMF.Api.Tests/Files/FileAuthorizationTests.cs (per-service authz + IDOR).
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

/// <summary>D-568 — the one service behind the centralized file store. Named
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

        // A6-18 (NCA) / D-494 — scan before storing; fail-closed in Production.
        await scanner.EnsureCleanFailClosedAsync(
            command.Content, command.OriginalFileName ?? "upload", command.FailClosed, cancellationToken);

        var sha256 = Convert.ToHexString(SHA256.HashData(command.Content)).ToLowerInvariant();
        var fileId = Guid.NewGuid();
        var write = await storage.WriteAsync(
            command.Service, fileId, detected.Extension, command.Content, policy.EncryptAtRest, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var file = new StoredFile
        {
            Id = fileId,
            Service = command.Service,
            SensitivityTier = policy.Tier,
            FileType = detected.Type,
            SourceType = FileSourceType.Upload,
            IsEncrypted = policy.EncryptAtRest,
            CipherFormatVersion = write.CipherFormatVersion,
            StorageKey = write.StorageKey,
            OriginalFileName = SanitizeFileName(command.OriginalFileName),
            ContentType = detected.ContentType,
            SizeBytes = command.Content.LongLength,
            Sha256 = sha256,
            IsDeletable = policy.DeletableDefault,
            RetainUntilUtc = policy.Retention is { } retention ? now.Add(retention) : null,
            OwnerEntityType = command.OwnerEntityType,
            OwnerEntityId = command.OwnerEntityId,
            CreatedBy = command.ActorUserId,
            CreatedAt = now,
            IsActive = true,
        };
        dbContext.StoredFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.FileUploaded,
            Outcome = AuditOutcome.Success,
            ActorUserId = command.ActorUserId,
            Detail = $"id={fileId}; service={command.Service}; type={detected.Type}; "
                + $"bytes={command.Content.LongLength}; encrypted={policy.EncryptAtRest}",
        }, cancellationToken);

        logger.LogInformation(
            "File {Id} uploaded (service={Service}, type={Type}, {Bytes} bytes, encrypted={Encrypted}).",
            fileId, command.Service, detected.Type, command.Content.LongLength, policy.EncryptAtRest);

        return new StoredFileResult(
            fileId, $"/api/v1/files/{fileId}", command.Service, detected.Type,
            policy.EncryptAtRest, command.Content.LongLength);
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
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.FileAccessDenied,
                Outcome = AuditOutcome.Failure,
                ActorUserId = caller.UserId,
                Detail = $"id={id}; service={file.Service}",
            }, cancellationToken);
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

        // P4 (D-568 hardening) — integrity verification on a private file (SAMA
        // H-29/30). FAIL CLOSED: a stored-hash mismatch means the bytes on disk
        // were tampered with (or a decrypt returned garbage) — audit it and refuse
        // to serve, never hand the caller unverified bytes. Only Confidential+
        // tiers carry the per-read hash check (public images are not hashed on read).
        if (policy.Tier >= FileSensitivityTier.Confidential && !string.IsNullOrEmpty(file.Sha256))
        {
            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(
                    "Integrity mismatch on file {Id} (service={Service}) — refusing to serve tampered bytes.",
                    id, file.Service);
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.FileIntegrityFailed,
                    Outcome = AuditOutcome.Failure,
                    ActorUserId = caller.UserId,
                    Detail = $"id={id}; service={file.Service}; expected={file.Sha256}; actual={actual}",
                }, cancellationToken);
                throw NotFound();
            }
        }

        // Per-row audit only for non-public reads (public reads would flood the log).
        if (policy.Access != FileAccessClass.Public)
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.FileDownloaded,
                Outcome = AuditOutcome.Success,
                ActorUserId = caller.UserId,
                Detail = $"id={id}; service={file.Service}",
            }, cancellationToken);
        }

        return new FileDownload(
            IsRedirect: false, RedirectUrl: null, Content: bytes,
            ContentType: file.ContentType ?? "application/octet-stream",
            FileName: file.OriginalFileName,
            file.Service, policy.Tier, policy.Access, file.FileType);
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

        var now = timeProvider.GetUtcNow();
        file.Deactivate();
        file.DeletedAt = now;
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.FileDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; service={file.Service}",
        }, cancellationToken);
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
            // P3 (D-568 hardening) — a ZIP container is an OOXML Office document.
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
