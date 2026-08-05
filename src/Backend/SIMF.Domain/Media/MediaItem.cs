using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Media;

/// <summary>
/// One item in the app's media gallery, an image or a video, managed from the
/// Control Panel and soft-deleted through <see cref="IsActive"/>.
///
/// <para>Binary bytes live outside this row, as they do for avatars and identity
/// documents. An uploaded image is addressed by <see cref="ImageFileId"/> in the
/// unified file store and streamed by the media endpoint; it is never a blob
/// column. An externally hosted video is referenced by <see cref="Url"/>
/// instead.</para>
/// </summary>
public class MediaItem : BaseAuditEntity
{
    /// <summary>Image or video; the public grid mixes both.</summary>
    public MediaKind Kind { get; set; }

    public string? Title { get; set; }
    public string? TitleArabic { get; set; }

    /// <summary>The uploaded image's row in the file store, and the source of
    /// truth for whether this item has an image. A bare Guid resolved on read,
    /// not a foreign key. Null when the item is an externally hosted video
    /// addressed by <see cref="Url"/>.</summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>Absolute URL of an externally hosted asset, typically the video.
    /// Null when the asset is an uploaded image.</summary>
    public string? Url { get; set; }

    /// <summary>A poster image for video tiles, held in the file store the same
    /// way. Null when the tile renders from <see cref="ImageFileId"/>
    /// directly.</summary>
    public Guid? ThumbnailFileId { get; set; }

    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }

    public int DisplayOrder { get; set; }
}
