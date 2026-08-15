namespace SIMF.Domain.Archive;

/// <summary>A past speaker snapshot on an archive edition, not a live Speaker row.
/// It has no active flag of its own: the parent edition's visibility governs.</summary>
public sealed class ArchivePastSpeaker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    public Guid? PhotoFileId { get; set; }

    public int? CountryId { get; set; }

    public int DisplayOrder { get; set; }
}
