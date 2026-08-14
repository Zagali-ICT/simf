// Tests: SIMF.Api.Tests/SpeakerPresentationsTests.cs
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Auditing;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>Admin management of speaker presentation
/// files. The bytes live in the unified
/// <c>StoredFile</c> store (Internal tier, encrypted at rest); the row keeps only
/// the metadata + the foreign key in <c>StoredFileId</c>. Both Speaker and
/// Session are real FKs on <see cref="SimfAppDbContext"/>, so the session title is
/// resolved in the same context.</summary>
internal sealed class AdminSpeakerPresentationService(
    SimfAppDbContext db,
    IFileService fileService,
    IFileStorageProvider fileStorage,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSpeakerPresentationService> logger) : IAdminSpeakerPresentationService
{
    private const long MaxFileBytes = 50 * 1024 * 1024; // 50 MB

    public async Task<IReadOnlyList<AdminSpeakerPresentationRow>> ListForSpeakerAsync(
        Guid speakerId, CancellationToken cancellationToken = default)
    {
        return await db.SpeakerPresentations.AsNoTracking()
            .Where(p => p.SpeakerId == speakerId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Join(db.Sessions.AsNoTracking(),
                p => p.SessionId, s => s.Id,
                (p, s) => new AdminSpeakerPresentationRow(
                    p.Id, p.SpeakerId, p.SessionId, s.Title, s.TitleArabic,
                    p.FileName, p.ContentType, p.SizeBytes, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminSpeakerPresentationRow> UploadAsync(
        Guid actorUserId, Guid speakerId, Guid sessionId,
        byte[] content, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        if (content is null || content.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.SpeakerPresentationInvalid, 400,
                "No file was uploaded.",
                "لم يتم رفع أي ملف.");
        }
        if (content.Length > MaxFileBytes)
        {
            throw new ApiException(
                ErrorCodes.SpeakerPresentationInvalid, 400,
                "The presentation file must be 50 MB or smaller.",
                "يجب ألا يتجاوز حجم ملف العرض 50 ميجابايت.");
        }
        // M2 (security) — accept only a real PDF / Office document, by extension
        // AND magic bytes, so an arbitrary (e.g. .html / script / executable)
        // file can't be stored behind the 50 MB size cap alone.
        if (!IsAllowedPresentation(content, fileName))
        {
            throw new ApiException(
                ErrorCodes.SpeakerPresentationInvalid, 400,
                "The presentation must be a PDF, PowerPoint, or Word document.",
                "يجب أن يكون ملف العرض بصيغة PDF أو PowerPoint أو Word.");
        }
        var speakerExists = await db.Speakers.AsNoTracking()
            .AnyAsync(s => s.Id == speakerId && s.IsActive, cancellationToken);
        if (!speakerExists)
        {
            throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدث.");
        }

        var session = await db.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new { s.Id, s.Title, s.TitleArabic })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        var safeName = SanitiseFileName(fileName);
        var presentationId = Guid.NewGuid();
        // Store the bytes in the unified StoredFile store (Internal tier,
        // encrypted at rest, owner = the presentation). IFileService runs the full
        // pipeline (malware scan, magic-byte allow-list, canonical MIME, SHA-256,
        // audit), so the standalone scanner call is gone. StoredFileId holds the
        // bare-Guid pointer to the StoredFile.
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.SpeakerPresentation, presentationId, content, safeName, contentType,
                actorUserId, FailClosed: false),
            cancellationToken);

        var presentation = new SpeakerPresentation
        {
            Id = presentationId,
            SpeakerId = speakerId,
            SessionId = sessionId,
            FileName = safeName,
            StoredFileId = result.Id,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream" : contentType,
            SizeBytes = content.Length,
            UploadedByUserId = actorUserId,
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        db.SpeakerPresentations.Add(presentation);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SpeakerPresentationUploaded,
            actorUserId,
            $"presentationId={presentation.Id}; speakerId={speakerId}; "
                + $"sessionId={sessionId}; file={safeName}; bytes={content.Length}",
            cancellationToken);

        logger.LogInformation(
            "Speaker presentation {File} uploaded for speaker {SpeakerId} session {SessionId} by {Actor}",
            safeName, speakerId, sessionId, actorUserId);

        return new AdminSpeakerPresentationRow(
            presentation.Id, speakerId, sessionId, session.Title, session.TitleArabic,
            safeName, presentation.ContentType, presentation.SizeBytes, presentation.CreatedAt);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid presentationId, CancellationToken cancellationToken = default)
    {
        var row = await db.SpeakerPresentations.AsNoTracking()
            .Where(p => p.Id == presentationId && p.IsActive)
            .Select(p => new { p.StoredFileId, p.ContentType, p.FileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }
        var bytes = await PresentationFileReader.ReadBytesAsync(
            db, fileStorage, row.StoredFileId, cancellationToken);
        return bytes is null ? null : (bytes, row.ContentType, row.FileName);
    }

    public async Task DeleteAsync(
        Guid actorUserId, Guid presentationId,
        CancellationToken cancellationToken = default)
    {
        var presentation = await db.SpeakerPresentations
            .SingleOrDefaultAsync(p => p.Id == presentationId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerPresentationNotFound, 404,
                "The presentation was not found.",
                "لم يتم العثور على العرض.");
        if (!presentation.IsActive)
        {
            return; // idempotent
        }

        presentation.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        // Retire the StoredFile (soft-delete + unlink bytes). Best-effort:
        // the row is already deactivated (source of truth), so a delete failure must
        // not fail the operation. SpeakerPresentation is DeletableDefault:true.
        try
        {
            await fileService.DeleteAsync(presentation.StoredFileId, actorUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "StoredFile retirement failed for presentation {PresentationId} (file {FileId}); the row is deactivated.",
                presentation.Id, presentation.StoredFileId);
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.SpeakerPresentationDeleted,
            actorUserId,
            $"presentationId={presentation.Id}; speakerId={presentation.SpeakerId}; "
                + $"file={presentation.FileName}",
            cancellationToken);
    }

    private static readonly HashSet<string> AllowedPresentationExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".pptx", ".ppt", ".docx", ".doc", ".xlsx", ".xls" };

    /// <summary>M2 (security) — true when the file looks like a real PDF, an
    /// OOXML/ZIP container (pptx/docx/xlsx) or a legacy OLE2 document
    /// (ppt/doc/xls): the extension is allow-listed AND the leading magic bytes
    /// match. Defeats a renamed / mislabelled upload (CWE-434).</summary>
    private static bool IsAllowedPresentation(byte[] content, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (!AllowedPresentationExtensions.Contains(ext))
        {
            return false;
        }
        // PDF — "%PDF".
        if (content.Length >= 4
            && content[0] == 0x25 && content[1] == 0x50
            && content[2] == 0x44 && content[3] == 0x46)
        {
            return true;
        }
        // OOXML (pptx/docx/xlsx) — a ZIP container, "PK\x03\x04".
        if (content.Length >= 4
            && content[0] == 0x50 && content[1] == 0x4B
            && content[2] == 0x03 && content[3] == 0x04)
        {
            return true;
        }
        // Legacy OLE2 (ppt/doc/xls) — D0 CF 11 E0 A1 B1 1A E1.
        if (content.Length >= 8
            && content[0] == 0xD0 && content[1] == 0xCF
            && content[2] == 0x11 && content[3] == 0xE0
            && content[4] == 0xA1 && content[5] == 0xB1
            && content[6] == 0x1A && content[7] == 0xE1)
        {
            return true;
        }
        return false;
    }

    private static string SanitiseFileName(string fileName)
    {
        // A3-14 (NCA App-Sec Standard) — Unicode-normalise the untrusted name
        // before stripping the path, so visually-identical but differently-encoded
        // sequences canonicalise to one form.
        var name = Path.GetFileName((fileName ?? string.Empty).Normalize(NormalizationForm.FormC)).Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = "presentation";
        }
        return name.Length > 256 ? name[^256..] : name;
    }
}
