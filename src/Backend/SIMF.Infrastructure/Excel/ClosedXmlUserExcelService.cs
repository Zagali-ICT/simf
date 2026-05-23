// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs (export round-trip, malformed input)
using ClosedXML.Excel;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// ClosedXML-backed implementation of the user-management Excel workbook
/// (decision D-044 b). Workbook layout:
///
/// <code>
///   Sheet 1 — "Users"
///   Row 1   — header: Email | DisplayName | State | Role | TwoFactor | CreatedAt
///   Row 2…  — one user per row
/// </code>
///
/// The Role column is "Administrator" or "User"; TwoFactor is "On" or "Off";
/// CreatedAt is an ISO-8601 string. Import accepts the same shape; the
/// State, TwoFactor and CreatedAt columns are ignored on import (the API
/// owns those values).
/// </summary>
internal sealed class ClosedXmlUserExcelService : IUserExcelService
{
    private const string SheetName = "Users";

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
            sheet.Cell(row, 1).Value = user.Email;
            sheet.Cell(row, 2).Value = user.DisplayName;
            sheet.Cell(row, 3).Value = user.AccountState;
            sheet.Cell(row, 4).Value = user.IsAdministrator ? "Administrator" : "User";
            sheet.Cell(row, 5).Value = user.TwoFactorEnabled ? "On" : "Off";
            sheet.Cell(row, 6).Value = user.CreatedAt.UtcDateTime.ToString("O");
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

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(xlsx));
        }
        catch (Exception)
        {
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }
        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name == SheetName)
                ?? workbook.Worksheets.FirstOrDefault()
                ?? throw new DataValidationException(
                    "The workbook has no worksheet.",
                    "لا توجد ورقة عمل في المصنف.");

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

            var rows = new List<UserImportRow>();
            var lastRow = sheet.LastRowUsed();
            if (lastRow is null) return rows;

            for (var r = 2; r <= lastRow.RowNumber(); r++)
            {
                var email = sheet.Cell(r, 1).GetString().Trim();
                var name = sheet.Cell(r, 2).GetString().Trim();
                var roleCell = sheet.Cell(r, 4).GetString().Trim();
                if (email.Length == 0 && name.Length == 0)
                {
                    continue;   // blank row — skip silently
                }
                rows.Add(new UserImportRow(
                    r, email, name,
                    string.Equals(roleCell, "Administrator", StringComparison.OrdinalIgnoreCase)));
            }
            return rows;
        }
    }
}
