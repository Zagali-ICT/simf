// Tests: SIMF.Api.Tests/PublicMediaTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Abstractions;
using SIMF.Application.Media.Abstractions;
using SIMF.Contracts.Media;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Media;

/// <summary>
/// D-199 (Mockup page 30) — public (anonymous) read of the active media
/// gallery. Mirrors <c>PublicDelegationService</c> but adds paging + an
/// optional album filter (the prompt: "active, paged, by album"). Image /
/// thumbnail bytes are fetched out-of-row via <see cref="IMediaImageStorage"/>.
/// </summary>
internal sealed class PublicMediaService(
    SimfAppDbContext dbContext,
    IMediaImageStorage imageStorage) : IPublicMediaService
{
    public async Task<PublicMediaPage> ListAsync(
        string? album, int skip, int top,
        CancellationToken cancellationToken = default)
    {
        var safeSkip = Math.Max(0, skip);
        var safeTop = Math.Clamp(top is > 0 ? top : 25, 1, 100);

        var rows = dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.IsActive);

        if (!string.IsNullOrWhiteSpace(album))
        {
            var albumTerm = album.Trim();
            rows = rows.Where(item => item.AlbumEn == albumTerm || item.AlbumAr == albumTerm);
        }

        var ordered = rows
            .OrderBy(item => item.DisplayOrder)
            .ThenByDescending(item => item.CreatedAt);

        var total = await ordered.CountAsync(cancellationToken);
        var pageRaw = await ordered
            .Skip(safeSkip)
            .Take(safeTop)
            .Select(item => new
            {
                item.Id,
                item.Kind,
                item.TitleEn,
                item.TitleAr,
                item.AlbumEn,
                item.AlbumAr,
                HasImage = item.ImageRelativePath != null,
                HasThumbnail = item.ThumbnailRelativePath != null,
                item.Url,
                item.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        var items = pageRaw
            .Select(row => new PublicMediaItem(
                row.Id,
                row.Kind,
                row.TitleEn,
                row.TitleAr,
                row.AlbumEn,
                row.AlbumAr,
                row.HasImage ? $"/media/{row.Id}/image" : null,
                row.HasThumbnail ? $"/media/{row.Id}/thumbnail" : null,
                row.Url,
                row.DisplayOrder))
            .ToList();

        return new PublicMediaPage(items, total, safeSkip, safeTop);
    }

    public async Task<(byte[] Content, string ContentType)?> GetImageAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var path = await dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.Id == id && item.IsActive)
            .Select(item => item.ImageRelativePath)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(path)) { return null; }
        return await imageStorage.GetAsync(path, cancellationToken);
    }

    public async Task<(byte[] Content, string ContentType)?> GetThumbnailAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var path = await dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.Id == id && item.IsActive)
            .Select(item => item.ThumbnailRelativePath)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(path)) { return null; }
        return await imageStorage.GetAsync(path, cancellationToken);
    }
}
