namespace SIMF.Application.Excel;

/// <summary>
/// One export column of a grid Excel workbook (D-356). The generic exporter
/// writes one sheet whose header row is the <see cref="Header"/> values and
/// whose data cells are produced by <see cref="Value"/> per row. This is the
/// single source for a resource's export layout — the same descriptor list a
/// resource declares once and the generic engine reuses for every page.
/// </summary>
/// <typeparam name="TRow">The grid summary row type being exported.</typeparam>
public sealed record GridExcelColumn<TRow>(string Header, Func<TRow, object?> Value);

/// <summary>
/// Renders a list of grid rows into an XLSX workbook (D-356). One hardened
/// implementation (<c>ClosedXmlGridExcelExporter</c>) carries the OWASP
/// formula-injection sanitisation (CWE-1236) so no per-resource exporter can
/// forget it.
/// </summary>
public interface IGridExcelExporter
{
    /// <summary>
    /// Builds a single-sheet workbook from <paramref name="rows"/> using the
    /// column descriptors. The sheet is named <paramref name="sheetName"/> so
    /// the matching importer can require it by exact name (no silent fallback).
    /// </summary>
    byte[] Export<TRow>(
        IEnumerable<TRow> rows,
        IReadOnlyList<GridExcelColumn<TRow>> columns,
        string sheetName);
}

/// <summary>One parsed data row of an import workbook — its 1-based sheet row
/// number and the trimmed cell values keyed by the (case-insensitive) header.</summary>
public sealed record GridImportRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);

/// <summary>The parsed data rows of an import workbook (header row excluded).</summary>
public sealed record GridImportSheet(IReadOnlyList<GridImportRow> Rows);

/// <summary>
/// Parses an uploaded XLSX workbook into typed rows for a generic grid import
/// (D-356). One hardened implementation (<c>ClosedXmlGridExcelImporter</c>)
/// enforces the strict sheet name, the required-header check and the row cap —
/// the size + ZIP-magic gate lives at the endpoint, matching the proven user
/// import (D-045 H1).
/// </summary>
public interface IGridExcelImporter
{
    /// <summary>
    /// Reads the worksheet named <paramref name="sheetName"/> (no fallback to
    /// the first sheet), verifies every header in <paramref name="requiredHeaders"/>
    /// is present, caps the data rows at <paramref name="maxRows"/>, and returns
    /// one <see cref="GridImportRow"/> per non-blank data row.
    /// </summary>
    /// <exception cref="SIMF.Common.DataValidationException">
    /// The bytes are not a valid workbook, the named sheet is missing, a
    /// required header is absent, or the row cap is exceeded.</exception>
    GridImportSheet Parse(
        byte[] xlsx,
        string sheetName,
        IReadOnlyList<string> requiredHeaders,
        int maxRows);
}
