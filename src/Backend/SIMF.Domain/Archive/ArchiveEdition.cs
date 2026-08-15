using SIMF.Domain.Common;

namespace SIMF.Domain.Archive;

/// <summary>
/// A previous SIMF edition, one row per forum year, shown on the public archive
/// screen and managed from the Control Panel. The public list is gated by the
/// archive-visibility operations toggle as well as by <see cref="BaseAuditEntity.IsActive"/>:
/// with the toggle off the endpoint returns nothing, however many active
/// editions exist.
/// </summary>
public class ArchiveEdition : BaseAuditEntity
{
    /// <summary>Unique across active and inactive rows alike — one edition per
    /// calendar year. Doubles as the natural display key and the default
    /// sort.</summary>
    public int Year { get; set; }

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>The theme line, such as "The third edition — Securing
    /// Tomorrow's Seas".</summary>
    public string? SummaryEn { get; set; }
    public string? SummaryAr { get; set; }

    // Reported totals for the edition, entered by an admin rather than counted.
    public int Attendees { get; set; }
    public int Sessions { get; set; }
    public int Speakers { get; set; }

    /// <summary>The edition's cover image, as its row in the one file store. A real foreign key:
    /// both sides live in the App database.
    ///
    /// <para>This was <c>CoverImageRelativePath</c>, admin-typed free text. An uploaded image and
    /// a linked one are now the same thing, a <c>StoredFile</c>, so the value is
    /// validated and stored once instead of living untyped on this row.</para>
    /// </summary>
    public Guid? CoverImageFileId { get; set; }

    /// <summary>The edition's venue, such as "الرياض · واجهة الرياض".</summary>
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }

    /// <summary>A human date label such as "November 2024 · 3 days", distinct
    /// from the numeric <see cref="Year"/>.</summary>
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }

    // Owned snapshot children, cascade-deleted with the edition.
    public ICollection<ArchiveMediaItem> Media { get; set; } = new List<ArchiveMediaItem>();
    public ICollection<ArchiveSessionTitle> SessionTitles { get; set; } = new List<ArchiveSessionTitle>();
    public ICollection<ArchivePastSpeaker> PastSpeakers { get; set; } = new List<ArchivePastSpeaker>();
}
