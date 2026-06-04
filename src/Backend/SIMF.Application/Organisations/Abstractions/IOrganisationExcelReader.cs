namespace SIMF.Application.Organisations.Abstractions;

/// <summary>One raw row read from the government organisations Excel
/// (.xlsx) workbook. <see cref="RowNumber"/> is the 1-based worksheet row
/// so import errors can name the exact line. All value columns are nullable
/// because the source sheet is human-maintained and often incomplete; the
/// importer is responsible for validating and trimming each value.</summary>
/// <param name="RowNumber">1-based worksheet row this data came from.</param>
/// <param name="NameAr">Arabic organisation name (required by the importer).</param>
/// <param name="NameEn">English organisation name, when present.</param>
/// <param name="CommercialRegistration">Commercial registration number, when present.</param>
/// <param name="Sector">Business sector, when present.</param>
/// <param name="City">City, when present.</param>
/// <param name="Phone">Contact phone, when present.</param>
/// <param name="Email">Contact e-mail, when present.</param>
/// <param name="Website">Web site URL, when present.</param>
public sealed record OrganisationImportRow(
    int RowNumber,
    string? NameAr,
    string? NameEn,
    string? CommercialRegistration,
    string? Sector,
    string? City,
    string? Phone,
    string? Email,
    string? Website);

/// <summary>Reads the government Saudi-companies lookup workbook into raw
/// <see cref="OrganisationImportRow"/> records. The infrastructure layer owns
/// the concrete ClosedXML implementation; the application layer stays
/// EF-free and parser-free and consumes only this abstraction.</summary>
public interface IOrganisationExcelReader
{
    /// <summary>Parses every data row of the supplied .xlsx stream into raw
    /// import rows, preserving worksheet order. The stream is read but not
    /// disposed by this method.</summary>
    /// <param name="xlsxStream">Open, readable .xlsx workbook stream.</param>
    /// <returns>The parsed rows, in worksheet order.</returns>
    IReadOnlyList<OrganisationImportRow> Read(Stream xlsxStream);
}
