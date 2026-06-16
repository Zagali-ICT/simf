namespace SIMF.Domain.Archive;

/// <summary>D-432 — one past speaker for an archive edition (Mockup screen
/// 24-01 "المتحدثون السابقون"). A snapshot child of <see cref="ArchiveEdition"/>:
/// parent FK only (no live Speaker FK — past editions' speakers are not live
/// rows), cascade-deleted with the edition, no own <c>IsActive</c> (inherits the
/// parent's visibility). The photo is a stored relative path, never an absolute
/// URL — matching Speaker / Sponsor.</summary>
public sealed class ArchivePastSpeaker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    /// <summary>English name. ≤ 128 chars.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic name — the primary surface. ≤ 128 chars.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Optional relative path to the speaker's photo (≤ 256 chars).</summary>
    public string? PhotoRelativePath { get; set; }

    /// <summary>Sort key within the edition's past-speaker row — ascending.</summary>
    public int DisplayOrder { get; set; }
}
