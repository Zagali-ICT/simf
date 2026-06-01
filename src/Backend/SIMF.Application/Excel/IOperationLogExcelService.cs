using SIMF.Contracts.Admin;

namespace SIMF.Application.Excel;

/// <summary>
/// P1.6 — builds the operation-log XLSX workbook. Export-only (the log is
/// never imported). Mirrors <see cref="IUserExcelService"/>; the
/// implementation reuses the same OWASP formula-injection guard (CWE-1236).
/// </summary>
public interface IOperationLogExcelService
{
    /// <summary>Builds an XLSX workbook from the given operation-log rows.</summary>
    byte[] Export(IEnumerable<AdminOperationLogSummary> rows);
}
