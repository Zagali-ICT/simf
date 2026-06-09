// Tests: SIMF.Api.Tests/ContactsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Contacts;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/contacts/export</c> — the D-356 grid export for the
/// shared Contact directory (SIMF-FDS-014). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// Contacts grid), and how to list + identify a contact row.
/// </summary>
public sealed class ExportContactsEndpoint(IAdminContactService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminContactSummary>(exporter)
{
    protected override string RoutePath => "/admin/contacts/export";
    protected override string Permission => PermissionCatalog.Contacts.Export;
    protected override string SheetName => "Contacts";
    protected override string FilePrefix => "simf-contacts";

    protected override IReadOnlyList<GridExcelColumn<AdminContactSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminContactSummary>> _columns =
    [
        new("NameAr", row => row.NameAr),
        new("NameEn", row => row.NameEn),
        new("PhonePrimary", row => row.PhonePrimary),
        new("Email", row => row.Email),
        new("CountryId", row => row.CountryId),
        new("CountryNameEn", row => row.CountryNameEn),
        new("CountryNameAr", row => row.CountryNameAr),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminContactSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAsync(query, ct)).Items;

    protected override Guid IdOf(AdminContactSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/contacts/import</c> — the D-356 grid import (insert-only,
/// the upsert under <c>Contacts.Edit</c>) for the shared Contact directory. The
/// base does the upload defence, parse and per-row error aggregation; this
/// subclass binds one row to <see cref="CreateContactRequest"/> and creates it
/// (the service throws <c>ApiException</c> for an invalid field — e.g. a too-long
/// name or a bad country — which the base records as a per-row error rather than
/// aborting the batch). <c>NameAr</c> is the only required column.
/// </summary>
public sealed class ImportContactsEndpoint(IAdminContactService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/contacts/import";
    protected override string Permission => PermissionCatalog.Contacts.Import;
    protected override string SheetName => "Contacts";
    protected override IReadOnlyList<string> RequiredHeaders => ["NameAr"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("NameAr", out var nameAr) ? nameAr : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var nameAr = row.Cells.GetValueOrDefault("NameAr", string.Empty);
        if (string.IsNullOrWhiteSpace(nameAr))
        {
            throw new DataValidationException(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.");
        }

        await service.CreateAsync(actorId, new CreateContactRequest
        {
            NameAr = nameAr,
            NameEn = NullIfBlank(row.Cells.GetValueOrDefault("NameEn", string.Empty)),
            PhonePrimary = NullIfBlank(row.Cells.GetValueOrDefault("PhonePrimary", string.Empty)),
            Email = NullIfBlank(row.Cells.GetValueOrDefault("Email", string.Empty)),
            CountryId = int.TryParse(
                row.Cells.GetValueOrDefault("CountryId", string.Empty), out var countryId)
                ? countryId
                : null,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}