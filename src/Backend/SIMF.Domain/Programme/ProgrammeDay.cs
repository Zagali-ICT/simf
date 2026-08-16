using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One day of the forum programme. Deliberately no foreign key from
/// <see cref="Session"/>: sessions are matched to a day by date, so either side
/// renders alone when the other is missing.
/// </summary>
public class ProgrammeDay : BaseAuditEntity
{
    public DateOnly Date { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Ascending, tie-broken by <see cref="Date"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Stamped when the day's rating batch is dispatched, so a restart
    /// cannot resend it.</summary>
    public DateTime? RatingPromptSent { get; set; }
}
