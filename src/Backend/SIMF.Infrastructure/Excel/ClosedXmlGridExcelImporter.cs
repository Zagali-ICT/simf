// Tests: SIMF.Api.Tests/InterestExcelTests.cs (import, wrong-sheet, not-a-workbook, duplicate)
using System.IO.Compression;
using ClosedXML.Excel;
using SIMF.Application.Excel;
using SIMF.Common;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// ClosedXML-backed generic grid importer. Mirrors the hardening of
/// <see cref="ClosedXmlUserExcelService"/> for every resource's XLSX import:
/// strict sheet-name match (no silent fallback to the first sheet), a
/// required-header check, and a row cap. The 5 MB size limit and the ZIP-magic
/// pre-check live at the endpoint (matching the user import); this
/// service assumes the bytes already passed that gate.
///
/// <para>Each data row becomes a case-insensitive header→trimmed-value
/// dictionary; the per-resource endpoint binds that into a typed create/upsert
/// request. Fully blank rows are skipped silently.</para>
/// </summary>
internal sealed class ClosedXmlGridExcelImporter : IGridExcelImporter
{
    public GridImportSheet Parse(
        byte[] xlsx,
        string sheetName,
        IReadOnlyList<string> requiredHeaders,
        int maxRows)
    {
        if (xlsx is null || xlsx.Length == 0)
        {
            throw new DataValidationException(
                "The Excel file is empty.",
                "ملف Excel فارغ.");
        }

        EnsureExpansionIsBounded(xlsx);

        using var stream = new MemoryStream(xlsx);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or FileFormatException)
        {
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name == sheetName)
                ?? throw new DataValidationException(
                    $"The workbook must contain a worksheet named '{sheetName}'.",
                    $"يجب أن يحتوي المصنف على ورقة عمل باسم '{sheetName}'.");

            // Build the header → column-index map from row 1 (trimmed, the
            // original casing kept as the dictionary key for the row cells).
            var headerRow = sheet.Row(1);
            var lastHeaderColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            var headerColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= lastHeaderColumn; c++)
            {
                var header = sheet.Cell(1, c).GetString().Trim();
                if (header.Length > 0 && !headerColumns.ContainsKey(header))
                {
                    headerColumns[header] = c;
                }
            }

            foreach (var required in requiredHeaders)
            {
                if (!headerColumns.ContainsKey(required))
                {
                    throw new DataValidationException(
                        $"The workbook is missing the required column '{required}'.",
                        $"المصنف لا يحتوي على العمود المطلوب '{required}'.");
                }
            }

            var rows = new List<GridImportRow>();
            var lastRow = sheet.LastRowUsed();
            if (lastRow is null) return new GridImportSheet(rows);

            var lastRowNumber = lastRow.RowNumber();
            if (lastRowNumber - 1 > maxRows)
            {
                throw new DataValidationException(
                    $"The workbook has more than {maxRows} data rows.",
                    $"يحتوي المصنف على أكثر من {maxRows} صفًا.");
            }

            for (var r = 2; r <= lastRowNumber; r++)
            {
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var anyValue = false;
                foreach (var (header, column) in headerColumns)
                {
                    var value = sheet.Cell(r, column).GetString().Trim();
                    cells[header] = value;
                    if (value.Length > 0) anyValue = true;
                }
                if (!anyValue) continue;   // blank row — skip silently
                rows.Add(new GridImportRow(r, cells));
            }
            return new GridImportSheet(rows);
        }
    }

    /// <summary>The maximum total UNCOMPRESSED size the workbook's parts may
    /// declare. The endpoint gate bounds the COMPRESSED upload at 5 MB, and an
    /// xlsx is a zip of xml: a sheet of a million rows sharing a handful of
    /// shared strings compresses far past that ratio, so a file that passes the
    /// size gate and the zip-magic pre-check can still allocate hundreds of
    /// megabytes while ClosedXML builds its object model - all of it before the
    /// maxRows check in Parse gets to reject anything. Generous enough that no legitimate
    /// import of the capped row count comes near it.</summary>
    private const long MaxUncompressedBytes = 200L * 1024 * 1024;

    /// <summary>Rejects a workbook whose parts declare more uncompressed bytes
    /// than <see cref="MaxUncompressedBytes"/>, BEFORE the object model is built.
    /// Reads only the zip directory, so it costs nothing on a normal file.</summary>
    private static void EnsureExpansionIsBounded(byte[] xlsx)
    {
        long declared = 0;
        try
        {
            using var probe = new MemoryStream(xlsx, writable: false);
            using var archive = new ZipArchive(probe, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                declared += entry.Length;
                if (declared > MaxUncompressedBytes)
                {
                    throw new DataValidationException(
                        "The workbook is too large to process.",
                        "المصنف كبير جدًا لمعالجته.");
                }
            }
        }
        catch (InvalidDataException)
        {
            // Not a readable zip at all - the same answer the workbook open
            // below would give, said one step earlier.
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }
    }
}
