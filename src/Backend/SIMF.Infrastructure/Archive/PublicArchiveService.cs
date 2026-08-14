// Tests: SIMF.Api.Tests/ArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Archive;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Archive;

/// <summary>Read-only public projection of active archive editions,
/// ordered newest-first for the Past Editions screen.
/// Gated by the archive-visibility operations toggle: when the toggle
/// is off the payload is empty, so the App / Website hide the screen without
/// a separate visibility round-trip.</summary>
internal sealed class PublicArchiveService(
    SimfAppDbContext appDbContext,
    IAssetService assetService,
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
                null,
                edition.LocationEn,
                edition.LocationAr,
                edition.DateLabelEn,
                edition.DateLabelAr))
            .ToListAsync(cancellationToken);

        // Which editions actually have a cover in the store — one batched query
        // for the page, no N+1. The wire's CoverImageRelativePath is retained but
        // always null, so HasCoverAsset is what a client branches on.
        var withCover = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.ArchiveCover,
            items.Select(edition => edition.Id).ToList(),
            cancellationToken);
        items = [.. items.Select(edition => edition with
        {
            HasCoverAsset = withCover.Contains(edition.Id),
        })];

        return new PublicArchive(items);
    }

    public async Task<PublicArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Same visibility gate as the list — a hidden archive
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
                null,
                // The rich child lists, each ordered by DisplayOrder.
                // AsSplitQuery (below) emits one query per collection; without it
                // the three sibling collection sub-selects JOIN into a single
                // Media×SessionTitles×PastSpeakers cartesian rowset.
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
            // Split the projection's collection sub-selects (see above). Safe:
            // single-row root (no Skip/Take) + each child carries an explicit
            // OrderBy(DisplayOrder), so every split query is deterministically
            // ordered and the wire output is byte-identical.
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
