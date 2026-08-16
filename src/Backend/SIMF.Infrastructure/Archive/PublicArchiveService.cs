// Tests: SIMF.Api.Tests/ArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Archive;
using SIMF.Infrastructure.Persistence;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Archive;

/// <summary>Read-only public projection of active archive editions,
/// ordered newest-first for the Past Editions screen.
/// Gated by the archive-visibility operations toggle: when the toggle
/// is off the payload is empty, so the App / Website hide the screen without
/// a separate visibility round-trip.</summary>
internal sealed class PublicArchiveService(
    SimfAppDbContext appDbContext,
    IAssetService assetService,
    IPublicApiOriginProvider origin,
    IOperationsToggleService operationsToggleService) : IPublicArchiveService
{
    // The archive's two child media routes. Built as a relative path in the
    // query and made ABSOLUTE afterwards, because the app tests these values
    // with isHttpUrl() before it will load them: a relative path is silently
    // dropped to the initials tile or the placeholder glyph, with nothing
    // logged and no test failing.
    // The serve route is /app/assets/{category}/{ownerId}/image - the trailing
    // segment is part of it, and a URL without it 404s while still looking
    // perfectly well-formed to the client's isHttpUrl check.
    private const string GalleryImagePath = "/app/assets/ArchiveGalleryImage/";
    private const string PastSpeakerPhotoPath = "/app/assets/ArchivePastSpeakerPhoto/";
    private const string AssetPathSuffix = "/image";

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

    /// <summary>Stamp the API's public origin onto the child media paths.
    ///
    /// <para>Done here rather than in the query because the origin is not a
    /// database value, and done at all because the app decides whether to render
    /// a past-speaker photo or a gallery still by asking whether the string is an
    /// absolute http(s) URL. A relative path fails that test silently — the card
    /// falls back to initials and the tile to a glyph, with no error anywhere —
    /// so shipped devices would simply stop showing archive images.</para></summary>
    private PublicArchiveEditionDetail WithAbsoluteMediaUrls(PublicArchiveEditionDetail detail)
    {
        var baseUrl = (origin.GetApiBaseUrl() ?? string.Empty).TrimEnd('/');
        if (baseUrl.Length == 0)
        {
            return detail;
        }

        return detail with
        {
            Gallery = detail.Gallery?
                .Select(item => item.Url.StartsWith(GalleryImagePath, StringComparison.Ordinal)
                    ? item with { Url = baseUrl + item.Url }
                    : item)
                .ToList(),
            PastSpeakers = detail.PastSpeakers?
                .Select(speaker => speaker.PhotoRelativePath is { } path
                        && path.StartsWith(PastSpeakerPhotoPath, StringComparison.Ordinal)
                    ? speaker with { PhotoRelativePath = baseUrl + path }
                    : speaker)
                .ToList(),
        };
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

        var detail = await appDbContext.ArchiveEditions.AsNoTracking()
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
                        (int)m.Kind,
                        // A video is the external link an admin supplied; an image
                        // is an uploaded file, addressed by the row's own id. The
                        // absolute origin is stamped in after the query, because
                        // the app gates on the string being an http(s) URL before
                        // it will load anything.
                        m.Kind == ArchiveMediaKind.Video
                            ? appDbContext.StoredFiles
                                .Where(f => f.Id == m.MediaFileId && f.IsActive)
                                .Select(f => f.ExternalUrl)
                                .FirstOrDefault() ?? string.Empty
                            : m.MediaFileId == null
                                ? string.Empty
                                : GalleryImagePath + m.Id + AssetPathSuffix,
                        m.CaptionEn, m.CaptionAr))
                    .ToList(),
                edition.SessionTitles
                    .OrderBy(s => s.DisplayOrder)
                    .Select(s => new PublicArchiveSessionTitle(s.TitleEn, s.TitleAr))
                    .ToList(),
                edition.PastSpeakers
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => new PublicArchivePastSpeaker(
                        p.NameEn, p.NameAr,
                        p.PhotoFileId == null
                            ? null
                            : PastSpeakerPhotoPath + p.Id + AssetPathSuffix,
                        p.CountryId))
                    .ToList()))
            // Split the projection's collection sub-selects (see above). Safe:
            // single-row root (no Skip/Take) + each child carries an explicit
            // OrderBy(DisplayOrder), so every split query is deterministically
            // ordered and the wire output is byte-identical.
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return detail is null ? detail : WithAbsoluteMediaUrls(detail);
    }
}
