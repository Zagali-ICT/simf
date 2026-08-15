using SIMF.Common.Enums;

namespace SIMF.Domain.Archive;

/// <summary>
/// One gallery item, photo or video, belonging to an archive edition. A snapshot
/// child of <see cref="ArchiveEdition"/>: it holds only the parent foreign key,
/// is cascade-deleted with the edition, and has no active flag of its own, since
/// the parent's visibility governs.
///
/// <para>These are historical media, held in the file store like everything else:
/// an image is uploaded bytes, a video an external link. Neither is ever resolved
/// against live speakers or sessions.</para>
/// </summary>
public sealed class ArchiveMediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public ArchiveMediaKind Kind { get; set; } = ArchiveMediaKind.Image;

    /// <summary>The item's media, as a row in <c>StoredFiles</c>: uploaded bytes
    /// for an image, an external link for a video. <see cref="Kind"/> says which,
    /// and picks the file service the write path uses.
    ///
    /// <para>Nullable because a row can exist before its file is attached — the
    /// same save-first shape every other media surface in the Control Panel
    /// has.</para></summary>
    public Guid? MediaFileId { get; set; }

    public string? CaptionEn { get; set; }
    public string? CaptionAr { get; set; }

    /// <summary>Ascending, within this edition's gallery.</summary>
    public int DisplayOrder { get; set; }
}
