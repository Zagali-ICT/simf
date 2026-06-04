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
/// by <see cref="ImageRelativePath"/> and streamed by the media image
/// endpoint, never stored as a blob column. An externally hosted video is
/// referenced by <see cref="Url"/> instead. <see cref="ThumbnailRelativePath"/>
/// optionally holds a poster image (also out-of-row) for video tiles.
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

    /// <summary>D-90 — relative path of the uploaded image's bytes within the
    /// media object store. Null when the item is an externally hosted video
    /// addressed by <see cref="Url"/>.</summary>
    public string? ImageRelativePath { get; set; }

    /// <summary>Absolute URL of an externally hosted asset (typically the
    /// video). Null when the asset is an uploaded image.</summary>
    public string? Url { get; set; }

    /// <summary>D-90 — relative path of an optional thumbnail / video poster
    /// image within the media object store. Null when the tile renders from
    /// <see cref="ImageRelativePath"/> directly.</summary>
    public string? ThumbnailRelativePath { get; set; }

    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }

    public int DisplayOrder { get; set; }
 }
