// Tests: SIMF.Api.Tests/ArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Operations.Abstractions;
using SIMF.Contracts.Archive;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Archive;

/// <summary>D-199 — read-only public projection of active archive editions,
/// ordered newest-first for the Past Editions screen (Mockup screen 24).
/// Gated by the archive-visibility operations toggle (D-166): when the toggle
/// is off the payload is empty, so the App / Website hide the screen without
/// a separate visibility round-trip.</summary>
internal sealed class PublicArchiveService(
    SimfAppDbContext appDbContext,
    IOperationsToggleService operationsToggleService) : IPublicArchiveService
{
    public async Task<PublicArchive> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var visibility = await operationsToggleService
            .GetArchiveVisibilityAsync(cancellationToken);
        if (!visibility.IsVisible)
        {
            return new PublicArchive([]);
        }

        var items = await appDbContext.ArchiveEditions.AsNoTracking()
            .Where(edition => edition.IsActive)
            .OrderByDescending(edition => edition.Year)
            .Select(edition => new PublicArchiveEdition(
                edition.Id,
                edition.Year,
                edition.TitleEn,
                edition.TitleAr,
                edition.SummaryEn,
                edition.SummaryAr,
                edition.Attendees,
                edition.Sessions,
                edition.Speakers,
                edition.CoverImageRelativePath,
                edition.LocationEn,
                edition.LocationAr,
                edition.DateLabelEn,
                edition.DateLabelAr))
            .ToListAsync(cancellationToken);

        return new PublicArchive(items);
    }

    public async Task<PublicArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // §9 (screen 24-01): same visibility gate as the list — a hidden archive
        // yields 404 at the endpoint (null here), not a leak of one edition.
        var visibility = await operationsToggleService
            .GetArchiveVisibilityAsync(cancellationToken);
        if (!visibility.IsVisible)
        {
            return null;
        }

        return await appDbContext.ArchiveEditions.AsNoTracking()
            .Where(edition => edition.IsActive && edition.Id == id)
            .Select(edition => new PublicArchiveEditionDetail(
                edition.Id,
                edition.Year,
                edition.TitleEn,
                edition.TitleAr,
                edition.SummaryEn,
                edition.SummaryAr,
                edition.LocationEn,
                edition.LocationAr,
                edition.DateLabelEn,
                edition.DateLabelAr,
                edition.Attendees,
                edition.Sessions,
                edition.Speakers,
                edition.CoverImageRelativePath,
                // D-432 — the rich child lists, ordered; EF projects these as a
                // single split query keyed on the FK index.
                edition.Media
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => new PublicArchiveMediaItem(
                        (int)m.Kind, m.Url, m.CaptionEn, m.CaptionAr))
                    .ToList(),
                edition.SessionTitles
                    .OrderBy(s => s.DisplayOrder)
                    .Select(s => new PublicArchiveSessionTitle(s.TitleEn, s.TitleAr))
                    .ToList(),
                edition.PastSpeakers
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => new PublicArchivePastSpeaker(
                        p.NameEn, p.NameAr, p.PhotoRelativePath, p.CountryId))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
