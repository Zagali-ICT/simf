// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs (export round-trip, malformed input,
//        formula-injection, wrong-sheet-name)
using ClosedXML.Excel;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// ClosedXML-backed implementation of the user-management Excel workbook.
///
/// <para>Workbook layout:</para>
/// <code>
///   Sheet 1 — must be named "Users" exactly (no silent fallback)
///   Row 1   — header: Email | DisplayName | State | Role | TwoFactor | CreatedAt
///   Row 2…  — one user per row, up to <see cref="MaxImportRows"/>
/// </code>
///
/// <para>Defence-in-depth:</para>
/// <list type="bullet">
///   <item><description>Export sanitises every string cell — values starting with
///     <c>=</c> / <c>+</c> / <c>-</c> / <c>@</c> / TAB / CR are prefixed with
///     an apostrophe so Excel does not auto-execute them. OWASP CSV/Formula
///     injection class (CWE-1236).</description></item>
///   <item><description>Import requires the sheet to be named "Users" exactly —
///     a workbook whose first sheet just happens to start with
///     <c>Email | DisplayName</c> is no longer silently consumed.</description></item>
///   <item><description>Import caps the row count — a 100k-row workbook stops at the cap
///     rather than tying up the API node for minutes.</description></item>
/// </list>
/// </summary>
internal sealed class ClosedXmlUserExcelService : IUserExcelService
{
    private const string SheetName = "Users";

    /// <summary>The maximum row count an import workbook may carry.</summary>
    public const int MaxImportRows = 5_000;

    public byte[] Export(IEnumerable<AdminUserSummary> users)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 3).Value = "State";
        sheet.Cell(1, 4).Value = "Role";
        sheet.Cell(1, 5).Value = "TwoFactor";
        sheet.Cell(1, 6).Value = "CreatedAt";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var user in users)
        {
            sheet.Cell(row, 1).Value = SanitiseForExcel(user.Email);
            sheet.Cell(row, 2).Value = SanitiseForExcel(user.DisplayName);
            sheet.Cell(row, 3).Value = SanitiseForExcel(user.AccountState);
            sheet.Cell(row, 4).Value = user.IsAdministrator ? "Administrator" : "User";
            sheet.Cell(row, 5).Value = user.TwoFactorEnabled ? "On" : "Off";
            sheet.Cell(row, 6).Value = user.CreatedAt.ToString("O");
            row++;
        }
        sheet.Columns(1, 6).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<UserImportRow> Parse(byte[] xlsx)
    {
        if (xlsx is null || xlsx.Length == 0)
        {
            throw new DataValidationException(
                "The Excel file is empty.",
                "ملف Excel فارغ.");
        }

        // Open the stream once; let `using` clean up even if XLWorkbook
        // throws partway through construction.
        using var stream = new MemoryStream(xlsx);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or System.IO.IOException
            or System.IO.FileFormatException)
        {
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }
        using (workbook)
        {
            // Strict sheet-name match — no silent fallback to the first
            // worksheet. A workbook whose first sheet just happens to have
            // the right header columns must NOT be accepted as a user list.
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name == SheetName)
                ?? throw new DataValidationException(
                    $"The workbook must contain a worksheet named '{SheetName}'.",
                    $"يجب أن يحتوي المصنف على ورقة عمل باسم '{SheetName}'.");

            // Header sanity check — Email + DisplayName are mandatory.
            var header1 = sheet.Cell(1, 1).GetString().Trim();
            var header2 = sheet.Cell(1, 2).GetString().Trim();
            if (!string.Equals(header1, "Email", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(header2, "DisplayName", StringComparison.OrdinalIgnoreCase))
            {
                throw new DataValidationException(
                    "The workbook header must start with Email then DisplayName.",
                    "يجب أن يبدأ رأس المصنف بـ Email ثم DisplayName.");
            }

            // Locate an optional ProfileTypeId column for the
            // Visitor / Other imports; -1 means the workbook has none
            // (e.g. the Admin import, which has never carried it).
            var profileTypeColumn = -1;
            var headerRow = sheet.Row(1);
            var lastHeaderColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var c = 1; c <= lastHeaderColumn; c++)
            {
                if (string.Equals(
                    sheet.Cell(1, c).GetString().Trim(),
                    "ProfileTypeId",
                    StringComparison.OrdinalIgnoreCase))
                {
                    profileTypeColumn = c;
                    break;
                }
            }

            var rows = new List<UserImportRow>();
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
                var email = sheet.Cell(r, 1).GetString().Trim();
                var name = sheet.Cell(r, 2).GetString().Trim();
                var roleCell = sheet.Cell(r, 4).GetString().Trim();
                if (email.Length == 0 && name.Length == 0)
                {
                    continue;   // blank row — skip silently
                }
                Guid? profileTypeId = null;
                if (profileTypeColumn > 0)
                {
                    var raw = sheet.Cell(r, profileTypeColumn).GetString().Trim();
                    if (raw.Length > 0 && Guid.TryParse(raw, out var parsed))
                    {
                        profileTypeId = parsed;
                    }
                }
                rows.Add(new UserImportRow(
                    r, email, name,
                    string.Equals(roleCell, "Administrator", StringComparison.OrdinalIgnoreCase),
                    profileTypeId));
            }
            return rows;
        }
    }

    /// <summary>
    /// Returns the value with a leading apostrophe prefix when it would be
    /// interpreted as a formula by Excel / LibreOffice / Sheets (OWASP CSV
    /// Injection — CWE-1236). The five risky leading characters are
    /// <c>= + - @</c> plus TAB and CR. The apostrophe is hidden in the cell
    /// but tells the spreadsheet to treat the contents as text.
    /// </summary>
    internal static string SanitiseForExcel(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var first = value[0];
        if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            return "'" + value;
        }
        return value;
    }
}
