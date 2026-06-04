// Tests: SIMF.Api.Tests/AdminAttendeesExportTests.cs
using ClosedXML.Excel;
using SIMF.Application.Excel;
using SIMF.Contracts.Admin;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// P1.6 — ClosedXML implementation of the attendee-roster export workbook.
/// Export-only. Every string cell is passed through
/// <see cref="ClosedXmlUserExcelService.SanitiseForExcel"/> — the same OWASP
/// formula-injection guard (CWE-1236) the user export uses — so there is one
/// hardened sanitiser, reused, not a copy.
/// </summary>
internal sealed class ClosedXmlAttendeeExcelService : IAttendeeExcelService
{
    private const string SheetName = "Attendees";

    public byte[] Export(IEnumerable<AdminAttendeeSummary> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 3).Value = "UserType";
        sheet.Cell(1, 4).Value = "ProfileType";
        sheet.Cell(1, 5).Value = "ProfileTypeArabic";
        sheet.Cell(1, 6).Value = "AccountState";
        sheet.Cell(1, 7).Value = "QrId";
        sheet.Cell(1, 8).Value = "CreatedAt";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var attendee in rows)
        {
            sheet.Cell(row, 1).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.Email);
            sheet.Cell(row, 2).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.DisplayName);
            sheet.Cell(row, 3).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.UserType);
            sheet.Cell(row, 4).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.ProfileTypeName);
            sheet.Cell(row, 5).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.ProfileTypeNameArabic);
            sheet.Cell(row, 6).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.AccountState);
            sheet.Cell(row, 7).Value = ClosedXmlUserExcelService.SanitiseForExcel(attendee.QrId);
            sheet.Cell(row, 8).Value = attendee.CreatedAt.UtcDateTime.ToString("O");
            row++;
        }
        sheet.Columns(1, 8).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
