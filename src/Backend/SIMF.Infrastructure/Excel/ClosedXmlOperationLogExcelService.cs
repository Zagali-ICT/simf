// Tests: SIMF.Api.Tests/AdminOperationLogExportTests.cs
using ClosedXML.Excel;
using SIMF.Application.Excel;
using SIMF.Contracts.Admin;

namespace SIMF.Infrastructure.Excel;

/// <summary>
/// P1.6 — ClosedXML implementation of the operation-log export workbook.
/// Export-only. Every string cell is passed through
/// <see cref="ClosedXmlUserExcelService.SanitiseForExcel"/> — the same OWASP
/// formula-injection guard (CWE-1236) the user export uses — so there is one
/// hardened sanitiser, reused, not a copy.
/// </summary>
internal sealed class ClosedXmlOperationLogExcelService : IOperationLogExcelService
{
    private const string SheetName = "OperationLog";

    public byte[] Export(IEnumerable<AdminOperationLogSummary> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        sheet.Cell(1, 1).Value = "Timestamp";
        sheet.Cell(1, 2).Value = "EventType";
        sheet.Cell(1, 3).Value = "Outcome";
        sheet.Cell(1, 4).Value = "SubjectEmail";
        sheet.Cell(1, 5).Value = "ActorUserId";
        sheet.Cell(1, 6).Value = "SourceIp";
        sheet.Cell(1, 7).Value = "CorrelationId";
        sheet.Cell(1, 8).Value = "ErrorCode";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var entry in rows)
        {
            sheet.Cell(row, 1).Value = entry.Timestamp.ToString("O");
            sheet.Cell(row, 2).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.EventType);
            sheet.Cell(row, 3).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.Outcome);
            sheet.Cell(row, 4).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.SubjectEmail);
            sheet.Cell(row, 5).Value = entry.ActorUserId?.ToString() ?? string.Empty;
            sheet.Cell(row, 6).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.SourceIp);
            sheet.Cell(row, 7).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.CorrelationId);
            sheet.Cell(row, 8).Value = ClosedXmlUserExcelService.SanitiseForExcel(entry.ErrorCode);
            row++;
        }
        sheet.Columns(1, 8).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
