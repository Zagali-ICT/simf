using SIMF.Contracts.Admin;

namespace SIMF.Application.Excel;

/// <summary>
/// P1.6 — builds the attendee-roster XLSX workbook. Export-only. Mirrors
/// <see cref="IUserExcelService"/>; the implementation reuses the same
/// OWASP formula-injection guard (CWE-1236).
/// </summary>
public interface IAttendeeExcelService
{
    /// <summary>Builds an XLSX workbook from the given attendee rows.</summary>
    byte[] Export(IEnumerable<AdminAttendeeSummary> rows);
}
