// Tests: SIMF.Api.Tests/PublicMediaTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Media.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Media;

/// <summary>
/// D-199 (Mockup page 30) — public (anonymous) read of the active media gallery.
/// Paged + optional album / kind filter. Image / thumbnail bytes now resolve from
/// the unified <c>StoredFile</c> store (D-568 Wave C S2) via the pointer columns
/// <c>MediaItem.ImageFileId</c> / <c>ThumbnailFileId</c>; the two serve routes,
/// the presence-only <c>imageUrl</c>/<c>thumbnailUrl</c> wire keys and the
/// content-type all stay byte-identical.
/// </summary>
internal sealed class PublicMediaService(
    SimfAppDbContext dbContext,
    IFileStorageProvider storage) : IPublicMediaService
{
    public async Task<PublicMediaPage> ListAsync(
        string? album, int skip, int top, MediaKind? kind = null,
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
            rows = rows.Where(item => item.Album == albumTerm || item.AlbumArabic == albumTerm);
        }

        // A8 — optional Kind (image / video) narrow. Kind is a mapped scalar, so
        // this translates server-side; absent (null) = both kinds (unchanged).
        if (kind is not null)
        {
            rows = rows.Where(item => item.Kind == kind.Value);
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
                item.Title,
                item.TitleArabic,
                item.Album,
                item.AlbumArabic,
                HasImage = item.ImageFileId != null,
                HasThumbnail = item.ThumbnailFileId != null,
                item.Url,
                item.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        var items = pageRaw
            .Select(row => new PublicMediaItem(
                row.Id,
                row.Kind,
                row.Title,
                row.TitleArabic,
                row.Album,
                row.AlbumArabic,
                row.HasImage ? $"/media/{row.Id}/image" : null,
                row.HasThumbnail ? $"/media/{row.Id}/thumbnail" : null,
                row.Url,
                row.DisplayOrder))
            .ToList();

        return new PublicMediaPage(items, total, safeSkip, safeTop);
    }

    public Task<(byte[] Content, string ContentType)?> GetImageAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        ResolveBytesAsync(
            dbContext.MediaItems.AsNoTracking()
                .Where(item => item.Id == id && item.IsActive)
                .Select(item => item.ImageFileId),
            cancellationToken);

    public Task<(byte[] Content, string ContentType)?> GetThumbnailAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        ResolveBytesAsync(
            dbContext.MediaItems.AsNoTracking()
                .Where(item => item.Id == id && item.IsActive)
                .Select(item => item.ThumbnailFileId),
            cancellationToken);

    // Resolve the pointed-at StoredFile's bytes + content-type. Returns null when
    // the item / pointer / file is missing (the endpoint maps that to 404) —
    // preserving the legacy "no bytes → 404" semantics.
    private async Task<(byte[] Content, string ContentType)?> ResolveBytesAsync(
        IQueryable<Guid?> fileIdQuery, CancellationToken cancellationToken)
    {
        var fileId = await fileIdQuery.SingleOrDefaultAsync(cancellationToken);
        if (fileId is not { } fid) { return null; }

        var file = await dbContext.StoredFiles
            .AsNoTracking()
            .Where(f => f.Id == fid && f.IsActive)
            .Select(f => new { f.StorageKey, f.ContentType, f.IsEncrypted })
            .FirstOrDefaultAsync(cancellationToken);
        if (file?.StorageKey is not { Length: > 0 } key) { return null; }

        var bytes = await storage.ReadAsync(key, file.IsEncrypted, cancellationToken);
        return bytes is null ? null : (bytes, file.ContentType ?? "application/octet-stream");
    }
}
