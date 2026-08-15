using SIMF.Domain.Common;

namespace SIMF.Domain.Archive;

/// <summary>A previous SIMF edition, one row per forum year. The public list is also
/// gated by the archive-visibility operations toggle, not only by IsActive.</summary>
public class ArchiveEdition : BaseAuditEntity
{
    public int Year { get; set; }

    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;

    public string? SummaryEn { get; set; }
    public string? SummaryAr { get; set; }

    // Reported totals, entered by an admin rather than counted.
    public int Attendees { get; set; }
    public int Sessions { get; set; }
    public int Speakers { get; set; }

    public Guid? CoverImageFileId { get; set; }

    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }

    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }

    public ICollection<ArchiveMediaItem> Media { get; set; } = new List<ArchiveMediaItem>();
    public ICollection<ArchiveSessionTitle> SessionTitles { get; set; } = new List<ArchiveSessionTitle>();
    public ICollection<ArchivePastSpeaker> PastSpeakers { get; set; } = new List<ArchivePastSpeaker>();
}
