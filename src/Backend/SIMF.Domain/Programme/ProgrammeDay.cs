using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One day of the forum programme: a calendar date with its own bilingual title
/// and a banner image. The app's Sessions screen groups sessions under the day
/// whose date matches the session's local start date, and shows the title and
/// banner above them.
///
/// <para>There is deliberately no foreign key from <see cref="Session"/> to this
/// entity. Sessions are matched to a day by date, so the two stay independently
/// editable: a day with no sessions still renders its title and banner, and a
/// session on a date with no day shows under a bare date header.</para>
/// </summary>
public class ProgrammeDay : BaseAuditEntity
{
    /// <summary>Unique across active days — one programme day per date.</summary>
    public DateOnly Date { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Ascending, tie-broken by <see cref="Date"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Stamped once the day's "please rate today" batch is dispatched,
    /// so a restart cannot resend it. The same guard
    /// <see cref="Session.RatingPromptSent"/> uses.</summary>
    public DateTime? RatingPromptSent { get; set; }
}
