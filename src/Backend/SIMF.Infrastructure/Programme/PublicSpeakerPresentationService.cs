// Tests: SIMF.Api.Tests/PublicPresentationsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>The public, read-only view behind "عروض الجلسات",
/// backing the app's "الجلسات" tile. Lists EVERY active session (time-ordered by
/// start so the app groups by day) with its primary speaker — NOT only the sessions
/// that happen to have an uploaded deck: the card opens the
/// session detail + AI summary, never the file, so a session with no presentation
/// still belongs on this list. When a session DOES carry an active presentation, its
/// id + file metadata ride along so the <c>/{id}/file</c> download route still
/// resolves; otherwise the item id falls back to the session id and the file fields
/// are empty (the app decodes them but the card ignores them). The bytes come
/// from the unified <c>StoredFile</c> store via <c>StoredFileName</c>.
/// Speaker + Session + Presentation are real FKs on <see cref="SimfAppDbContext"/>.</summary>
internal sealed class PublicSpeakerPresentationService(
    SimfAppDbContext db,
    IFileStorageProvider fileStorage) : IPublicSpeakerPresentationService
{
    public async Task<PublicPresentations> ListAsync(
        CancellationToken cancellationToken = default)
    {
        // One row per active session. The primary speaker is the lowest-ordered
        // SessionSpeaker; the optional presentation is the first active deck on the
        // session (its id keeps the file download working where one exists).
        var rows = await db.Sessions.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Start)
            .ThenBy(s => s.Title)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.TitleArabic,
                s.Start,
                Speaker = s.Speakers
                    .OrderBy(ss => ss.DisplayOrder)
                    .Select(ss => new { ss.Speaker!.Name, ss.Speaker.NameArabic })
                    .FirstOrDefault(),
                Presentation = db.SpeakerPresentations
                    .Where(p => p.SessionId == s.Id && p.IsActive)
                    .OrderBy(p => p.FileName)
                    .Select(p => new { p.Id, p.FileName, p.ContentType, p.SizeBytes })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new PublicPresentationItem(
                r.Presentation is not null ? r.Presentation.Id : r.Id,
                r.Id,
                r.Title,
                r.TitleArabic,
                r.Start,
                r.Speaker?.Name ?? string.Empty,
                r.Speaker?.NameArabic ?? string.Empty,
                r.Presentation?.FileName ?? string.Empty,
                r.Presentation?.ContentType ?? string.Empty,
                r.Presentation?.SizeBytes ?? 0))
            .ToList();

        return new PublicPresentations(items);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid presentationId, CancellationToken cancellationToken = default)
    {
        var row = await db.SpeakerPresentations.AsNoTracking()
            .Where(p => p.Id == presentationId && p.IsActive && p.Session!.IsActive)
            .Select(p => new { p.StoredFileName, p.ContentType, p.FileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var bytes = await PresentationFileReader.ReadBytesAsync(
            db, fileStorage, row.StoredFileName, cancellationToken);
        return bytes is null ? null : (bytes, row.ContentType, row.FileName);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid sessionId, Guid presentationId, CancellationToken cancellationToken = default)
    {
        // The session scope is the authorisation for the anonymous website route:
        // the presentation must belong to THIS session (and both be active).
        var row = await db.SpeakerPresentations.AsNoTracking()
            .Where(p => p.Id == presentationId && p.SessionId == sessionId
                && p.IsActive && p.Session!.IsActive)
            .Select(p => new { p.StoredFileName, p.ContentType, p.FileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var bytes = await PresentationFileReader.ReadBytesAsync(
            db, fileStorage, row.StoredFileName, cancellationToken);
        return bytes is null ? null : (bytes, row.ContentType, row.FileName);
    }
}
