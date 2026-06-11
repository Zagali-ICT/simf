// Tests: SIMF.Api.Tests/OrganisationExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Organisations;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/organisations/export</c> — the D-356 grid export for
/// the Saudi-companies lookup. <b>Export only:</b> Organisations keeps its
/// bespoke government-Excel bulk <c>Import</c> (<see cref="PermissionCatalog.Organisations.Import"/>,
/// <c>OrganisationImportResult</c>), so no generic import endpoint is added here.
/// </summary>
public sealed class ExportOrganisationsEndpoint(IAdminOrganisationService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminOrganisationSummary>(exporter)
{
    protected override string RoutePath => "/admin/organisations/export";
    protected override string Permission => PermissionCatalog.Organisations.Export;
    protected override string SheetName => "Organisations";
    protected override string FilePrefix => "simf-organisations";

    protected override IReadOnlyList<GridExcelColumn<AdminOrganisationSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminOrganisationSummary>> _columns =
    [
        new("NameAr", row => row.NameAr),
        new("NameEn", row => row.NameEn),
        new("CommercialRegistration", row => row.CommercialRegistration),
        new("Sector", row => row.Sector),
        new("City", row => row.City),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminOrganisationSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAsync(query, ct)).Items;

    protected override Guid IdOf(AdminOrganisationSummary row) => row.Id;
}
