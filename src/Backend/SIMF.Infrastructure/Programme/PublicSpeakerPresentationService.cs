// Tests: SIMF.Api.Tests/PublicPresentationsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>Wave 2 (Figma 1388:7621 "عروض الجلسات") — the public, read-only view
/// of speaker-presentation files for the app. Lists every active presentation on
/// an active session (with the presenting speaker), time-ordered by session start
/// so the app groups by day. D-568 (Wave C S6): the bytes come from the unified
/// <c>StoredFile</c> store via <c>StoredFileName</c> (a bare-Guid pointer). Speaker
/// + Session are real FKs on <see cref="SimfAppDbContext"/>, so both are resolved
/// in the same query.</summary>
internal sealed class PublicSpeakerPresentationService(
    SimfAppDbContext db,
    IFileStorageProvider fileStorage) : IPublicSpeakerPresentationService
{
    public async Task<PublicPresentations> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await db.SpeakerPresentations.AsNoTracking()
            .Where(p => p.IsActive && p.Session!.IsActive)
            .OrderBy(p => p.Session!.StartUtc)
            .ThenBy(p => p.FileName)
            .Select(p => new PublicPresentationItem(
                p.Id,
                p.SessionId,
                p.Session!.Title,
                p.Session.TitleArabic,
                p.Session.StartUtc,
                p.Speaker!.Name,
                p.Speaker.NameArabic,
                p.FileName,
                p.ContentType,
                p.SizeBytes))
            .ToListAsync(cancellationToken);

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
}
