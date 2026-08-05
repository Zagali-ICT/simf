using SIMF.Common.Enums;

namespace SIMF.Domain.Archive;

/// <summary>
/// One gallery item, photo or video, belonging to an archive edition. A snapshot
/// child of <see cref="ArchiveEdition"/>: it holds only the parent foreign key,
/// is cascade-deleted with the edition, and has no active flag of its own, since
/// the parent's visibility governs.
///
/// <para>These are historical media, so <see cref="Url"/> is stored as given and
/// never resolved against live speakers or sessions.</para>
/// </summary>
public sealed class ArchiveMediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public ArchiveMediaKind Kind { get; set; } = ArchiveMediaKind.Image;

    /// <summary>A path under the media root for an image, or a URL for a
    /// video.</summary>
    public string Url { get; set; } = string.Empty;

    public string? CaptionEn { get; set; }
    public string? CaptionAr { get; set; }

    /// <summary>Ascending, within this edition's gallery.</summary>
    public int DisplayOrder { get; set; }
}
