namespace SIMF.Domain.Archive;

/// <summary>
/// One session title from a past edition's programme, and a snapshot child of
/// <see cref="ArchiveEdition"/>. It holds only the parent foreign key, because a
/// past edition's sessions are not live rows, and it is cascade-deleted with the
/// edition. It has no active flag of its own; the parent's visibility governs.
/// </summary>
public sealed class ArchiveSessionTitle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    public string TitleEn { get; set; } = string.Empty;

    /// <summary>The primary surface.</summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Ascending, within this edition's programme.</summary>
    public int DisplayOrder { get; set; }
}
