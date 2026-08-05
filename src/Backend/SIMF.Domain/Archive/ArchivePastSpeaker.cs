namespace SIMF.Domain.Archive;

/// <summary>
/// One past speaker of an archive edition, and a snapshot child of
/// <see cref="ArchiveEdition"/>. It holds only the parent foreign key, because a
/// past edition's speakers are not live speaker rows, and it is cascade-deleted
/// with the edition. It has no active flag of its own; the parent's visibility
/// governs.
/// </summary>
public sealed class ArchivePastSpeaker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public string NameEn { get; set; } = string.Empty;

    /// <summary>The primary surface.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>A path under the media root, never an absolute URL — the same
    /// convention live speakers and sponsors use.</summary>
    public string? PhotoRelativePath { get; set; }

    /// <summary>ISO 3166-1 numeric, driving the corner flag in the app. No
    /// navigation: names are resolved on read, as they are for a live
    /// speaker.</summary>
    public int? CountryId { get; set; }

    /// <summary>Ascending, within this edition.</summary>
    public int DisplayOrder { get; set; }
}
