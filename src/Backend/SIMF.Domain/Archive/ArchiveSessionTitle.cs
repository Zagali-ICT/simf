namespace SIMF.Domain.Archive;

/// <summary>D-432 — one programme/session title for an archive edition
/// (Mockup screen 24-01 "عناوين الجلسات"). A snapshot child of
/// <see cref="ArchiveEdition"/>: parent FK only (no live Session FK — past
/// editions' sessions are not live rows), cascade-deleted with the edition,
/// no own <c>IsActive</c> (inherits the parent's visibility).</summary>
public sealed class ArchiveSessionTitle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArchiveEditionId { get; set; }
    public ArchiveEdition? Edition { get; set; }

    /// <summary>English title. ≤ 200 chars.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Arabic title — the primary surface. ≤ 200 chars.</summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Sort key within the edition's programme — ascending.</summary>
    public int DisplayOrder { get; set; }
}
