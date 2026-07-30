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
/// matching UTC instant; callers never deal in UTC.</para>
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
/// date string: the shared XLSX exporter writes a raw <c>DateTimeOffset</c> as
/// UTC, which would put a UTC timestamp in a workbook that must show local time.
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
