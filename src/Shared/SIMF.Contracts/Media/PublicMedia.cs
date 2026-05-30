using SIMF.Common.Enums;

namespace SIMF.Contracts.Media;

/// <summary>D-199 — public (anonymous) gallery page payload (Mockup page 30).
/// Paged and optionally narrowed to one album. <c>Items</c> are active only.</summary>
public sealed record PublicMediaPage(
    IReadOnlyList<PublicMediaItem> Items,
    int Total,
    int Skip,
    int Top);

/// <summary>D-199 — one public gallery tile. <c>ImageUrl</c> is the absolute
/// path the App requests for the tile bitmap: for an uploaded image it points
/// at <c>/media/{id}/image</c>; for an external video it is the item's own
/// poster/thumbnail endpoint when present, else null. <c>VideoUrl</c> carries
/// the external playback URL for video items.</summary>
public sealed record PublicMediaItem(
    Guid Id,
    MediaKind Kind,
    string? TitleEn,
    string? TitleAr,
    string? AlbumEn,
    string? AlbumAr,
    string? ImageUrl,
    string? ThumbnailUrl,
    string? VideoUrl,
    int DisplayOrder);
