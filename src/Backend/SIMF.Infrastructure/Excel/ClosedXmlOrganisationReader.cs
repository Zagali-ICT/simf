// Tests: SIMF.Api.Tests/OrganisationTests.cs
using ClosedXML.Excel;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Common;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// ClosedXML-backed reader for the government-supplied Saudi-companies
/// lookup workbook (bilingual Arabic / English). <b>Parse-only</b> — it never
/// touches the database; <see cref="SIMF.Infrastructure.Organisations.AdminOrganisationService"/> owns the
/// insert/update decisions over the rows this reader returns.
///
/// <para>Workbook layout:</para>
/// <code>
///   Sheet 1 — the first worksheet is used (no fixed name; gov files vary)
///   Row 1   — header row; columns are matched by name, in any order
///   Row 2…  — one organisation per row; fully-empty rows are skipped
///   Row cap - at most <see cref="MaxImportRows"/> data rows
/// </code>
///
/// <para>Header matching is case- AND whitespace-insensitive and accepts a
/// set of English + Arabic aliases per field. A header that is missing leaves
/// that field <c>null</c> for every row — the reader is deliberately
/// defensive because the government export format is not under our control.</para>
/// </summary>
internal sealed class ClosedXmlOrganisationReader : IOrganisationExcelReader
{
    /// <summary>
    /// Accepted header aliases per logical field. Keys are normalised by
    /// <see cref="Normalise"/> (lower-cased, whitespace removed) so the lookup
    /// is case- and whitespace-insensitive. Both English and Arabic spellings
    /// are listed for each field.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, OrganisationField> HeaderAliases =
        BuildHeaderAliases();

    /// <summary>The maximum row count an import workbook may carry, matching the
    /// user importer. Both siblings guard this loop and this one did not: a real
    /// government companies export runs to tens of thousands of rows, and the
    /// caller then issues one existence lookup PER ROW on a single request
    /// thread, so an uncapped file ties up the API node instead of failing
    /// cleanly with a bilingual 400.</summary>
    public const int MaxImportRows = 5_000;

    /// <summary>
    /// Reads every data row from the first worksheet of <paramref name="xlsxStream"/>
    /// and maps the matched columns onto <see cref="OrganisationImportRow"/> values.
    /// Returns an empty list when the workbook has no worksheet or no data rows.
    /// </summary>
    /// <param name="xlsxStream">The .xlsx workbook stream.</param>
    public IReadOnlyList<OrganisationImportRow> Read(Stream xlsxStream)
    {
        using var workbook = new XLWorkbook(xlsxStream);

        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null) return new List<OrganisationImportRow>();

        // Build a field -> column-number map from the header row. A field that
        // never appears in the header keeps its default (0) and is treated as
        // "missing" (null for every row) when the data is read.
        var columns = MapHeaderColumns(sheet);

        var rows = new List<OrganisationImportRow>();
        var lastRow = sheet.LastRowUsed();
        if (lastRow is null) return rows;

        var lastRowNumber = lastRow.RowNumber();
        if (lastRowNumber - 1 > MaxImportRows)
        {
            throw new DataValidationException(
                $"The workbook has more than {MaxImportRows} data rows.",
                $"يحتوي المصنف على أكثر من {MaxImportRows} صفًا.");
        }

        for (var r = 2; r <= lastRowNumber; r++)
        {
            var nameAr = ReadCell(sheet, r, columns, OrganisationField.NameAr);
            var nameEn = ReadCell(sheet, r, columns, OrganisationField.NameEn);
            var commercialRegistration = ReadCell(sheet, r, columns, OrganisationField.CommercialRegistration);
            var sector = ReadCell(sheet, r, columns, OrganisationField.Sector);
            var city = ReadCell(sheet, r, columns, OrganisationField.City);
            var phone = ReadCell(sheet, r, columns, OrganisationField.Phone);
            var email = ReadCell(sheet, r, columns, OrganisationField.Email);
            var website = ReadCell(sheet, r, columns, OrganisationField.Website);

            // Skip fully-empty rows — gov exports often carry trailing blanks.
            if (nameAr is null
                && nameEn is null
                && commercialRegistration is null
                && sector is null
                && city is null
                && phone is null
                && email is null
                && website is null)
            {
                continue;
            }

            rows.Add(new OrganisationImportRow(
                r,
                nameAr,
                nameEn,
                commercialRegistration,
                sector,
                city,
                phone,
                email,
                website));
        }

        return rows;
    }

    /// <summary>
    /// Walks the header row (row 1) and returns a map from each recognised
    /// logical field to its 1-based column number. Unrecognised header cells
    /// are ignored; a field that is absent simply does not appear in the map.
    /// </summary>
    private static IReadOnlyDictionary<OrganisationField, int> MapHeaderColumns(IXLWorksheet sheet)
    {
        var columns = new Dictionary<OrganisationField, int>();

        var headerRow = sheet.Row(1);
        var lastHeaderColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var c = 1; c <= lastHeaderColumn; c++)
        {
            var key = Normalise(sheet.Cell(1, c).GetString());
            if (key.Length == 0) continue;
            if (!HeaderAliases.TryGetValue(key, out var field)) continue;

            // First column wins if an alias is duplicated across headers.
            if (!columns.ContainsKey(field))
            {
                columns[field] = c;
            }
        }

        return columns;
    }

    /// <summary>
    /// Returns the trimmed string value of the cell for <paramref name="field"/>
    /// on the given row, or <c>null</c> when the field is missing from the
    /// workbook or the cell is empty / whitespace.
    /// </summary>
    private static string? ReadCell(
        IXLWorksheet sheet,
        int rowNumber,
        IReadOnlyDictionary<OrganisationField, int> columns,
        OrganisationField field)
    {
        if (!columns.TryGetValue(field, out var column)) return null;

        var value = sheet.Cell(rowNumber, column).GetString().Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Normalises a header cell for alias matching: lower-cased (invariant)
    /// with every Unicode whitespace character removed, so "Commercial
    /// Registration", "commercialregistration" and " CR " all collapse to a
    /// single comparable key.
    /// </summary>
    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch)) continue;
            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the normalised alias -> field map. Each alias is run through
    /// <see cref="Normalise"/> so the keys match the normalised header cells
    /// produced at read time.
    /// </summary>
    private static IReadOnlyDictionary<string, OrganisationField> BuildHeaderAliases()
    {
        var raw = new (OrganisationField Field, string[] Aliases)[]
        {
            (OrganisationField.NameAr, new[]
            {
                "namear", "name", "arabic name", "الاسم", "اسم الجهة", "اسم",
            }),
            (OrganisationField.NameEn, new[]
            {
                "nameen", "english name", "name (en)", "الاسم بالانجليزية", "الاسم الانجليزي",
            }),
            (OrganisationField.CommercialRegistration, new[]
            {
                "cr", "commercialregistration", "commercial registration", "السجل التجاري", "سجل تجاري",
            }),
            (OrganisationField.Sector, new[]
            {
                "sector", "القطاع", "قطاع",
            }),
            (OrganisationField.City, new[]
            {
                "city", "المدينة", "مدينة",
            }),
            (OrganisationField.Phone, new[]
            {
                "phone", "mobile", "tel", "الهاتف", "هاتف", "جوال",
            }),
            (OrganisationField.Email, new[]
            {
                "email", "e-mail", "البريد", "البريد الالكتروني",
            }),
            (OrganisationField.Website, new[]
            {
                "website", "url", "الموقع", "الموقع الالكتروني",
            }),
        };

        var map = new Dictionary<string, OrganisationField>();
        foreach (var (field, aliases) in raw)
        {
            foreach (var alias in aliases)
            {
                var key = Normalise(alias);
                if (key.Length == 0) continue;

                // First field to claim an alias wins; "name" maps to NameAr.
                if (!map.ContainsKey(key))
                {
                    map[key] = field;
                }
            }
        }

        return map;
    }

    /// <summary>The logical columns this reader recognises in the workbook.</summary>
    private enum OrganisationField
    {
        NameAr,
        NameEn,
        CommercialRegistration,
        Sector,
        City,
        Phone,
        Email,
        Website,
    }
}
