// Tests: SIMF.Api.Tests/ConfigurationExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/system-settings/export</c> — the D-356 grid export for
/// System Configuration (P2.4 / D-229). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// Configuration grid), and how to list + identify a system-setting row.
/// </summary>
public sealed class ExportConfigurationEndpoint(IAdminSystemSettingService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSystemSettingSummary>(exporter)
{
    protected override string RoutePath => "/admin/system-settings/export";
    protected override string Permission => PermissionCatalog.Configuration.Export;
    protected override string SheetName => "Configuration";
    protected override string FilePrefix => "simf-configuration";

    protected override IReadOnlyList<GridExcelColumn<AdminSystemSettingSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSystemSettingSummary>> _columns =
    [
        new("Key", row => row.Key),
        new("Value", row => row.Value),
        new("Description", row => row.Description),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminSystemSettingSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAsync(query, ct)).Items;

    protected override Guid IdOf(AdminSystemSettingSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/system-settings/import</c> — the D-356 grid import
/// (insert-only) for System Configuration. The base does the upload defence,
/// parse and per-row error aggregation; this subclass binds one row to
/// <see cref="AdminCreateSystemSettingRequest"/> and creates it (the service
/// rejects a duplicate active key with a <c>409</c> → a per-row error, not a
/// batch abort).
/// </summary>
public sealed class ImportConfigurationEndpoint(IAdminSystemSettingService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/system-settings/import";
    protected override string Permission => PermissionCatalog.Configuration.Import;
    protected override string SheetName => "Configuration";
    protected override IReadOnlyList<string> RequiredHeaders => ["Key", "Value"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Key", out var key) ? key : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var key = row.Cells.GetValueOrDefault("Key", string.Empty);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DataValidationException(
                "The setting key is required.",
                "مفتاح الإعداد مطلوب.");
        }

        await service.CreateAsync(actorId, new AdminCreateSystemSettingRequest
        {
            Key = key,
            Value = row.Cells.GetValueOrDefault("Value", string.Empty),
            Description = NullIfBlank(row.Cells.GetValueOrDefault("Description", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
