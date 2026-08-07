// Tests: SIMF.Api.Tests/InterestExcelTests.cs (export round-trip via the reference resource)
using ClosedXML.Excel;
using SIMF.Application.Excel;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// ClosedXML-backed generic grid exporter. One hardened renderer for
/// every resource's XLSX export, so the OWASP CSV/formula-injection guard
/// (CWE-1236) and the layout conventions live in exactly one place rather than
/// being copy-pasted into 39 per-entity services.
///
/// <para>Workbook layout: a single sheet named for the resource; row 1 is the
/// bold header (the column descriptors' <c>Header</c> values); rows 2… are one
/// grid row each. Every string cell is run through
/// <see cref="ClosedXmlUserExcelService.SanitiseForExcel"/> so a value starting
/// with <c>= + - @</c> / TAB / CR cannot auto-execute in Excel.</para>
/// </summary>
internal sealed class ClosedXmlGridExcelExporter : IGridExcelExporter
{
    public byte[] Export<TRow>(
        IEnumerable<TRow> rows,
        IReadOnlyList<GridExcelColumn<TRow>> columns,
        string sheetName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c].Header;
        }
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                SetCell(sheet.Cell(row, c + 1), columns[c].Value(item));
            }
            row++;
        }

        if (columns.Count > 0)
        {
            sheet.Columns(1, columns.Count).AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Writes a typed value into a cell. Strings are sanitised against formula
    /// injection; booleans become Yes/No, dates ISO-8601 (round-trips through
    /// the importer); numbers stay numeric so the spreadsheet can sum/sort them.
    /// </summary>
    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case string s:
                cell.Value = ClosedXmlUserExcelService.SanitiseForExcel(s);
                break;
            case bool b:
                cell.Value = b ? "Yes" : "No";
                break;
            // One arm, not two: DateTimeOffset and DateTime used to be distinct
            // cases and are now the same type (owner decision 2026-07-31 — every
            // instant is a plain Saudi-local DateTime).
            case DateTime dt:
                cell.Value = dt.ToString("O");
                break;
            case Guid g:
                cell.Value = g.ToString();
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                cell.Value = Convert.ToDouble(value);
                break;
            case float or double or decimal:
                cell.Value = Convert.ToDouble(value);
                break;
            default:
                cell.Value = ClosedXmlUserExcelService.SanitiseForExcel(value.ToString());
                break;
        }
    }
}
