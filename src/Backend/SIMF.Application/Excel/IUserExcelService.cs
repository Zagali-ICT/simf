using SIMF.Contracts.Authentication;

namespace SIMF.Application.Excel;

/// <summary>
/// Builds and parses the user-management Excel workbook (decision D-044 b).
/// The workbook has one sheet "Users" with the columns Email, DisplayName,
/// State, Role, TwoFactor, CreatedAt. Export rounds-trips through Import.
/// </summary>
public interface IUserExcelService
{
    /// <summary>Builds an XLSX workbook from the given user summaries.</summary>
    byte[] Export(IEnumerable<AdminUserSummary> users);

    /// <summary>
    /// Parses an XLSX workbook into import rows. Throws
    /// <see cref="SIMF.Common.DataValidationException"/> when the workbook is
    /// not a valid XLSX or the required sheet/header is missing. Per-row
    /// content validation (duplicate emails, missing names) is the caller's
    /// concern — this layer only returns the parsed rows.
    /// </summary>
    IReadOnlyList<UserImportRow> Parse(byte[] xlsx);
}

/// <summary>A row parsed from an import workbook. The row keeps its source
/// row number so the per-row error report can point a human at the failure.
///
/// <para>D-113: <see cref="ProfileTypeId"/> is read from an optional
/// <c>ProfileTypeId</c> header column when the workbook carries one
/// (the Other-kind import needs it; the Admin-kind import never
/// emits it). Null when the column is absent or the cell is empty /
/// unparseable.</para></summary>
public sealed record UserImportRow(
    int RowNumber,
    string Email,
    string DisplayName,
    bool IsAdministrator,
    Guid? ProfileTypeId = null);
