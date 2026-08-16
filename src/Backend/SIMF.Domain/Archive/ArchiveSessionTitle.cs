namespace SIMF.Domain.Archive;

/// <summary>A session title snapshot on a past edition, not a live Session row.
/// It has no active flag of its own: the parent edition's visibility governs.</summary>
public sealed class ArchiveSessionTitle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
