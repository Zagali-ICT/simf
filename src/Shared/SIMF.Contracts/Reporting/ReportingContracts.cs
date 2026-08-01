using SIMF.Common;

namespace SIMF.Contracts.Reporting;

/// <summary>
/// A server-paged report request: the standard grid contract plus the report
/// period.
///
/// <para>Composition rather than inheritance because <see cref="GridQuery"/> is
/// <c>sealed</c> and is shared by around forty resources — widening it for the
/// reports would change the request shape everywhere.</para>
///
/// <para><see cref="From"/> and <see cref="To"/> are <b>Saudi calendar dates</b>
/// and the range is <b>inclusive on both ends</b>, which is what an operator
/// picking "6 to 8 November" means. The service converts each end to the
/// matching Saudi-local instant. Nothing anywhere in the chain is a zoned value (D-813).</para>
/// </summary>
public sealed class ReportQuery
{
    /// <summary>Paging, sorting, free-text search and per-column filters.</summary>
    public GridQuery Grid { get; set; } = new();

    /// <summary>First Saudi day included. Null means "no lower bound".</summary>
    public DateOnly? From { get; set; }

    /// <summary>Last Saudi day included. Null means "no upper bound".</summary>
    public DateOnly? To { get; set; }
}

/// <summary>
/// One session's attendance. <c>Attendees</c> is a <b>distinct</b> person count,
/// so an attendee who re-enters the hall counts once.
/// </summary>
public sealed record AttendanceReportRow(
    Guid SessionId,
    string Code,
    string Title,
    string HallName,
    string StartDisplay,
    int Attendees,
    int LiveNow);

/// <summary>
/// One registered account. <c>RegisteredDisplay</c> is a pre-formatted Saudi
/// date string, so the shared XLSX exporter cannot re-interpret it — a workbook
/// is user-facing data and must read in Saudi local time.
/// </summary>
public sealed record RegistrationReportRow(
    Guid UserId,
    string DisplayName,
    string Email,
    string ProfileTypeName,
    string AccountState,
    string RegisteredDisplay);

/// <summary>One recorded gate scan, allowed or denied.</summary>
public sealed record GateActivityReportRow(
    long ScanId,
    string GateName,
    string ScannedDisplay,
    string Direction,
    string Outcome,
    string? DenialReason,
    string? VisitorName,
    string? ProfileTypeName);

/// <summary>
/// One programme session with its speakers, attendance and audience score.
/// <c>Speakers</c> is a pre-joined display string rather than a collection: a
/// report row is a spreadsheet row, and a nested list has no cell to live in.
/// </summary>
public sealed record SessionsReportRow(
    Guid SessionId,
    string Code,
    string Title,
    string HallName,
    string StartDisplay,
    string Speakers,
    int Attendees,
    int Questions,
    string AverageRating);

/// <summary>
/// One submitted rating. <c>TargetId</c> is deliberately left as an opaque id:
/// what it points at depends on the rating type's scope (a session, a day, the
/// whole forum), and resolving it per scope would mean a join per row.
/// </summary>
public sealed record RatingsReportRow(
    Guid ResponseId,
    string RatingTypeName,
    string Scope,
    Guid TargetId,
    int? OverallStars,
    string? Comment,
    string SubmittedDisplay);

/// <summary>
/// One partner organisation, flattened across the three kinds the Control Panel
/// manages separately (exhibitor, sponsor, booth) so an organiser can read the
/// whole partner surface in one contact list instead of three pages.
/// </summary>
public sealed record PartnersReportRow(
    Guid Id,
    string Kind,
    string Name,
    string NameArabic,
    string? Tier,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    bool IsActive);

/// <summary>
/// One meeting request, flattened across the speaker and delegation kinds. Both
/// carry the same operational shape (who asked, of whom, for when, and how it
/// was answered), so they belong in one report.
/// </summary>
public sealed record MeetingsReportRow(
    Guid Id,
    string Kind,
    string Requester,
    string Target,
    string Subject,
    string SlotDisplay,
    string Status,
    string RequestedDisplay,
    bool CheckedIn);

/// <summary>One audience question asked in a session.</summary>
public sealed record EngagementReportRow(
    Guid QuestionId,
    string SessionCode,
    string SessionTitle,
    string QuestionText,
    string Recipient,
    string Status,
    string Phase,
    bool IsHidden,
    string AskedDisplay);

/// <summary>A report page plus the totals that describe the whole filtered set,
/// not just the visible page. The header figures must not change when the
/// operator turns the page.</summary>
public sealed record ReportPage<TRow>(
    IReadOnlyList<TRow> Rows,
    int Total,
    int Skip,
    int Top,
    IReadOnlyList<ReportTotal> Totals);

/// <summary>One labelled headline figure shown above a report grid. The label is
/// a resource KEY, resolved by the Control Panel, so the API stays
/// language-neutral.</summary>
public sealed record ReportTotal(string LabelKey, string Value);
