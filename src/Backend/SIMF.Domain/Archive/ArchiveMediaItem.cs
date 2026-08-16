using SIMF.Common.Enums;

namespace SIMF.Domain.Archive;

/// <summary>A gallery item snapshot on an archive edition, never resolved against live
/// rows. It has no active flag of its own: the parent edition's visibility governs.</summary>
public sealed class ArchiveMediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public ArchiveMediaKind Kind { get; set; } = ArchiveMediaKind.Image;

    /// <summary>Uploaded bytes for an image, an external link for a video;
    /// <see cref="Kind"/> picks which file service the write path uses.</summary>
    public Guid? MediaFileId { get; set; }

    public string? CaptionEn { get; set; }
    public string? CaptionAr { get; set; }

    public int DisplayOrder { get; set; }
}
