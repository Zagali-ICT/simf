using SIMF.Common.Enums;

namespace SIMF.Domain.Archive;

/// <summary>D-432 — one gallery item (photo or video) for an archive edition
/// (Mockup screen 24-01 "الصور والفيديو"). A snapshot child of
/// <see cref="ArchiveEdition"/>: parent FK only, cascade-deleted with the
/// edition, with no own <c>IsActive</c> (visibility inherits the parent). These
/// are historical media, so <see cref="Url"/> is a stored relative path (image)
/// or an external/relative URL (video) — never resolved against live entities.</summary>
public sealed class ArchiveMediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning edition — the only relation (no Speaker/Session FK).</summary>
    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public ArchiveMediaKind Kind { get; set; } = ArchiveMediaKind.Image;

    /// <summary>Relative path (image) or URL (video). ≤ 512 chars.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional bilingual caption (≤ 256 chars each).</summary>
    public string? CaptionEn { get; set; }
    public string? CaptionAr { get; set; }

    /// <summary>Sort key within the edition's gallery — ascending.</summary>
    public int DisplayOrder { get; set; }
}
