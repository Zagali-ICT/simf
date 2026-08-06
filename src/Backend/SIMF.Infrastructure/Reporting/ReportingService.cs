// Tests: SIMF.Api.Tests/ReportingTests.cs
using SIMF.Application.Excel;
using SIMF.Application.Reporting.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Reporting;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// The Control Panel reporting module. Read-only throughout: it aggregates
/// records the other contexts own and never writes.
///
/// <para>Split across partial-class files, one per report, so no single file
/// carries every query.</para>
///
/// <para>Two rules apply to every report here:</para>
/// <list type="number">
///   <item>A date range is a range of <b>Saudi calendar days</b>, inclusive on
///   both ends, resolved once to a half-open window. The stored
///   instants ARE the Saudi wall clock, so no zone shift happens — but the
///   inclusive upper end still has to become the start of the FOLLOWING day, or
///   every report silently drops the last day, which is the day people check
///   first.</item>
///   <item>Every date a row carries is a <b>pre-formatted Saudi string</b>, not a
///   <c>DateTime</c>, so the shared XLSX renderer cannot re-interpret it.</item>
/// </list>
/// </summary>
internal sealed partial class ReportingService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IGridExcelExporter excelExporter) : IReportingService
{
    /// <summary>
    /// Hard ceiling on an export. A report over an event-scale audit log could
    /// otherwise try to materialise millions of rows into one workbook and take
    /// the process down. When the filtered set is larger the export returns the
    /// first <see cref="ExportRowCap"/> rows in the report's sort order rather
    /// than failing, and the caller is expected to narrow the date range.
    /// </summary>
    private const int ExportRowCap = 20_000;

    /// <summary>
    /// A report period as a half-open window <c>[Start, End)</c> in Saudi local
    /// time. Both ends are optional: an open end means "no bound in that
    /// direction".
    /// </summary>
    private readonly record struct ReportWindow(DateTime? Start, DateTime? End);

    /// <summary>
    /// Turns the requested Saudi date range into window bounds. This
    /// is a relabel, not a conversion — <c>SaudiTime.FromSaudiWallClock</c> only
    /// stamps the <c>Kind</c>, because the stored value already IS the Saudi wall
    /// clock. It is kept as the single named seam so the intent stays explicit.
    /// <paramref name="query"/>'s <c>To</c> is the last day the operator wants
    /// INCLUDED, so the exclusive upper bound is the start of the following
    /// Saudi day. Getting that wrong silently drops the final day of every
    /// report, which is the day people check first.
    /// </summary>
    private static ReportWindow ResolveWindow(ReportQuery query) =>
        new(
            query.From is { } from
                ? SaudiTime.FromSaudiWallClock(from.ToDateTime(TimeOnly.MinValue))
                : null,
            query.To is { } to
                ? SaudiTime.FromSaudiWallClock(to.AddDays(1).ToDateTime(TimeOnly.MinValue))
                : null);

    /// <summary>Normalises paging. Delegates to <see cref="GridQuery.ClampPage"/>
    /// so the reports cannot drift from the bounds every other grid uses.</summary>
    private static (int Skip, int Top) ResolvePaging(ReportQuery query) =>
        query.Grid.ClampPage();

    /// <summary>The free-text search term, or null when the caller sent none.</summary>
    private static string? SearchTerm(ReportQuery query) =>
        string.IsNullOrWhiteSpace(query.Grid.Search) ? null : query.Grid.Search.Trim();

    /// <summary>A headline figure, thousands-separated. Invariant culture: the
    /// Control Panel renders the string as given, and the API stays
    /// language-neutral.</summary>
    private static string Figure(int value) =>
        value.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Renders a report to XLSX. Sheet names are the report slug so the matching
    /// import (where one exists) can require it by exact name.
    /// </summary>
    private byte[] Export<TRow>(
        IReadOnlyList<TRow> rows,
        IReadOnlyList<GridExcelColumn<TRow>> columns,
        string sheetName) =>
        excelExporter.Export(rows, columns, sheetName);
}
