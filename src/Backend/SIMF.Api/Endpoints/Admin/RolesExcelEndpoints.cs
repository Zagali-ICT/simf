// Tests: SIMF.Api.Tests/RolesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/roles/export</c> — the generic grid export for the RBAC
/// roles resource. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout (mirroring the roles grid), and how to list + identify a role.
/// </summary>
public sealed class ExportRolesEndpoint(IAdminRoleService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminRoleSummary>(exporter)
{
    protected override string RoutePath => "/admin/roles/export";
    protected override string Permission => PermissionCatalog.Roles.Export;
    protected override string SheetName => "Roles";
    protected override string FilePrefix => "simf-roles";

    protected override IReadOnlyList<GridExcelColumn<AdminRoleSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminRoleSummary>> _columns =
    [
        new("Name", row => row.Name),
        new("IsBaseline", row => row.IsBaseline),
        new("UserCount", row => row.UserCount),
        new("PermissionCount", row => row.PermissionCount),
    ];

    protected override async Task<IReadOnlyList<AdminRoleSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminRoleSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/roles/import</c> — the generic grid import (insert-only)
/// for RBAC roles. The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="AdminCreateRoleRequest"/>
/// and creates a custom role (the service rejects a duplicate name / a baseline
/// clash / an invalid length → a per-row error, not a batch abort). The only
/// create input a role takes is its <c>Name</c>, so that is the sole required
/// header.
/// </summary>
public sealed class ImportRolesEndpoint(IAdminRoleService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/roles/import";
    protected override string Permission => PermissionCatalog.Roles.Import;
    protected override string SheetName => "Roles";
    protected override IReadOnlyList<string> RequiredHeaders => ["Name"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Name", out var name) ? name : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var name = row.Cells.GetValueOrDefault("Name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DataValidationException(
                "The role name is required.",
                "اسم الدور مطلوب.");
        }

        await service.CreateAsync(actorId, new AdminCreateRoleRequest
        {
            Name = name,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
