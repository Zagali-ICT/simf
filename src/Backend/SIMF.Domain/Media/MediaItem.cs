using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Media;

/// <summary>
/// D-199 (Mockup page 30 — معرض الصور والفيديوهات) — one item in the App
/// media gallery: an image or a video. Admin-managed CRUD; bilingual title
/// + album; soft-deleted via <see cref="IsActive"/>.
///
/// Binary image / poster bytes live OUTSIDE this row (decision D-90, the
/// same policy as avatars and id-documents): an uploaded image is addressed
/// by <see cref="ImageFileId"/> (a row in the unified StoredFile store) and
/// streamed by the media image endpoint, never stored as a blob column. An
/// externally hosted video is referenced by <see cref="Url"/> instead.
/// <see cref="ThumbnailFileId"/> optionally holds a poster image (also
/// out-of-row) for video tiles.
///
/// Shape mirrors <c>SIMF.Domain.Programme.Speaker</c>: a plain POCO with
/// public setters, <c>CreatedAt</c>/<c>UpdatedAt</c> audit stamps,
/// <c>DisplayOrder</c> and <c>IsActive</c> (no base class — Speaker does not
/// use one either).
/// </summary>
public class MediaItem:BaseAuditEntity
{
     /// <summary>image | video (Mockup page 30 mixes both in one grid).</summary>
    public MediaKind Kind { get; set; }

    public string? Title { get; set; }
    public string? TitleArabic { get; set; }

    /// <summary>D-568 (Wave C S2) — the uploaded image's row in the unified
    /// <c>StoredFile</c> store (a bare Guid logical reference, no FK — D-157;
    /// resolved on read). Null when the item is an externally hosted video
    /// addressed by <see cref="Url"/>. The new source of truth for "has image".</summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>Absolute URL of an externally hosted asset (typically the
    /// video). Null when the asset is an uploaded image.</summary>
    public string? Url { get; set; }

    /// <summary>D-568 (Wave C S2) — the thumbnail / poster's row in the unified
    /// <c>StoredFile</c> store (bare Guid, no FK). Null when the tile renders from
    /// <see cref="ImageFileId"/> directly.</summary>
    public Guid? ThumbnailFileId { get; set; }

    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }

    public int DisplayOrder { get; set; }
 }
