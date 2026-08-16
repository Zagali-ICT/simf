using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Media;

/// <summary>One item in the app's media gallery. Bytes never live on this row: an image is a
/// file-store row streamed by the media endpoint, a video a file-store row holding an external
/// link rather than bytes.</summary>
public class MediaItem : BaseAuditEntity
{
    public MediaKind Kind { get; set; }

    public string? Title { get; set; }
    public string? TitleArabic { get; set; }

    /// <summary>Source of truth for whether this item has an image. Null when the
    /// item is an externally hosted video addressed by <see cref="VideoFileId"/>.</summary>
    public Guid? ImageFileId { get; set; }

    public Guid? VideoFileId { get; set; }

    /// <summary>A poster image for video tiles. Nothing writes this column today,
    /// but the <c>thumbnailUrl</c> it feeds is decoded by the shipped app as the
    /// first branch of the gallery tile, and that wire key is append-only.</summary>
    public Guid? ThumbnailFileId { get; set; }

    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }

    public int DisplayOrder { get; set; }
}
