// Tests: SIMF.Api.Tests/GatesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/gates/export</c> — the D-356 grid export for gates.
/// All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>; this
/// subclass only declares the route, permission, sheet/file names, the column
/// layout, and how to list + identify a gate row.
/// </summary>
public sealed class ExportGatesEndpoint(IAdminGateService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminGateSummary>(exporter)
{
    protected override string RoutePath => "/admin/gates/export";
    protected override string Permission => PermissionCatalog.Gates.Export;
    protected override string SheetName => "Gates";
    protected override string FilePrefix => "simf-gates";

    protected override IReadOnlyList<GridExcelColumn<AdminGateSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminGateSummary>> _columns =
    [
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("DirectionMode", row => row.DirectionMode),
        new("AllowedProfileTypeCount", row => row.AllowedProfileTypeCount),
        new("AssignedOperatorCount", row => row.AssignedOperatorCount),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminGateSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminGateSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/gates/import</c> — the D-356 grid import (insert-only).
/// The base does the upload defence, parse and per-row error aggregation; this
/// subclass binds one row to <see cref="AdminCreateGateRequest"/> and creates it
/// (the service rejects a duplicate code or an invalid field → a per-row error,
/// not a batch abort).
/// </summary>
public sealed class ImportGatesEndpoint(IAdminGateService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/gates/import";
    protected override string Permission => PermissionCatalog.Gates.Import;
    protected override string SheetName => "Gates";
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DataValidationException(
                "The gate code is required.",
                "رمز البوابة مطلوب.");
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

        await service.CreateAsync(actorId, new AdminCreateGateRequest
        {
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            Description = row.Cells.GetValueOrDefault("Description", string.Empty) is { Length: > 0 } description
                ? description
                : null,
            DescriptionArabic = row.Cells.GetValueOrDefault("DescriptionArabic", string.Empty) is { Length: > 0 } descriptionArabic
                ? descriptionArabic
                : null,
            DirectionMode = Enum.TryParse<DirectionMode>(
                row.Cells.GetValueOrDefault("DirectionMode", string.Empty), ignoreCase: true, out var mode)
                ? mode
                : DirectionMode.Both,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
