// Tests: SIMF.Api.Tests/CountriesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Common.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/countries/export</c> — the D-356 grid export for the
/// Countries lookup. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout, and how to list a country row.
/// <para>Country's primary key is an <see cref="int"/> (ISO 3166-1 numeric),
/// not the <see cref="Guid"/> the generic export contract carries, so the CP
/// page always exports the current filtered set (it never sends selected Guid
/// ids) and <see cref="IdOf"/> is unused.</para>
/// </summary>
public sealed class ExportCountriesEndpoint(IAdminCountryService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminCountrySummary>(exporter)
{
    protected override string RoutePath => "/admin/countries/export";
    protected override string Permission => PermissionCatalog.Countries.Export;
    protected override string SheetName => "Countries";
    protected override string FilePrefix => "simf-countries";

    protected override IReadOnlyList<GridExcelColumn<AdminCountrySummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminCountrySummary>> _columns =
    [
        new("Id", row => row.Id),
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("PhonePrefix", row => row.PhonePrefix),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminCountrySummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    // Country ids are int (ISO numeric), not the Guid the generic export
    // contract carries; the CP page always exports the filtered set, so IdOf
    // never participates in a selected-ids filter.
    protected override Guid IdOf(AdminCountrySummary row) => Guid.Empty;
}

/// <summary>
/// <c>POST /api/v1/admin/countries/import</c> — the D-356 grid import
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="AdminCreateCountryRequest"/>
/// and creates it (the service rejects a duplicate id/code and any invalid field
/// with an <c>ApiException</c>, which the base records as a per-row error rather
/// than aborting the batch).
/// </summary>
public sealed class ImportCountriesEndpoint(IAdminCountryService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/countries/import";
    protected override string Permission => PermissionCatalog.Countries.Import;
    protected override string SheetName => "Countries";
    protected override IReadOnlyList<string> RequiredHeaders => ["Id", "Code", "Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var idText = row.Cells.GetValueOrDefault("Id", string.Empty);
        if (!int.TryParse(idText, out var id) || id <= 0)
        {
            throw new DataValidationException(
                "The country id must be a positive integer (ISO 3166-1 numeric).",
                "يجب أن يكون معرّف البلد عدداً صحيحاً موجباً (ISO 3166-1).");
        }

        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DataValidationException(
                "The country code is required.",
                "رمز البلد مطلوب.");
        }

        var name = row.Cells.GetValueOrDefault("Name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DataValidationException(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.");
        }

        var nameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(nameArabic))
        {
            throw new DataValidationException(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.");
        }

        var phonePrefix = row.Cells.GetValueOrDefault("PhonePrefix", string.Empty);
        await service.CreateAsync(actorId, new AdminCreateCountryRequest
        {
            Id = id,
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            PhonePrefix = string.IsNullOrWhiteSpace(phonePrefix) ? null : phonePrefix,
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
