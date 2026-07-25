using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// D-452 (Figma 883:2308 "تفاصيل اليوم") — one day of the forum programme:
/// a calendar <see cref="Date"/> with its own bilingual <see cref="Title"/> and
/// a logo / banner image (the D-357 <c>ProgrammeDayImage</c> asset, owned by this
/// row's <c>Id</c>). The app's Sessions screen groups <see cref="Session"/>s under
/// the day whose <see cref="Date"/> matches the session's local start date and
/// shows the day title + banner above that day's sessions.
///
/// <para>There is intentionally <b>no FK</b> from <see cref="Session"/> to
/// <see cref="ProgrammeDay"/>: sessions are matched to a day by date, so the two
/// stay independently editable (a day with no sessions still renders its title +
/// banner; a session on a date with no day shows under a bare date header).</para>
/// </summary>
public class ProgrammeDay : BaseAuditEntity
{
    /// <summary>The calendar date this day represents (event-local). Unique
    /// across active days — one programme day per date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>English day title shown in the "تفاصيل اليوم" banner.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Arabic title — paired with <see cref="Title"/>.</summary>
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Order among the programme days (ascending); ties break on
    /// <see cref="Date"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Once-only guard for the end-of-day rating prompt: stamped by
    /// <c>ProgrammeRatingPromptWorker</c> after the day's "please rate today"
    /// batch is dispatched so a restart cannot resend (D-679; mirrors
    /// <see cref="Session.RatingPromptSent"/>).</summary>
    public DateTimeOffset? RatingPromptSent { get; set; }
}
