// Tests: SIMF.Api.Tests/VipsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/vips/export</c> — the D-356 grid export for the
/// public-relations VIP list (D-168, PDF §2.7.3). <b>Export only:</b> the VIP
/// list is a derived view of <c>UserProfile</c> rows (those whose
/// <c>ProfileType.Name</c> is in {VVIP, VIP, Gold}) — there is nothing to import
/// here, and the page's only action is the bulk-notify modal. Columns mirror the
/// grid (English + Arabic name, job title, profile type, email). Lists via the
/// SAME service method the list endpoint uses
/// (<see cref="IAdminInvitationService.ListVipsAsync"/>); the row id is the
/// <c>UserProfileId</c> (the grid's row key).
/// </summary>
public sealed class ExportVipsEndpoint(IAdminInvitationService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminVipSummary>(exporter)
{
    protected override string RoutePath => "/admin/vips/export";
    protected override string Permission => PermissionCatalog.Vips.Export;
    protected override string SheetName => "VIPs";
    protected override string FilePrefix => "simf-vips";

    protected override IReadOnlyList<GridExcelColumn<AdminVipSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminVipSummary>> _columns =
    [
        new("EnglishName", row => row.EnglishName),
        new("ArabicName", row => row.ArabicName),
        new("JobTitle", row => row.JobTitle ?? string.Empty),
        new("ProfileType", row => row.ProfileTypeName),
        new("ProfileTypeArabic", row => row.ProfileTypeNameArabic),
        new("Email", row => row.Email ?? string.Empty),
    ];

    protected override async Task<IReadOnlyList<AdminVipSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListVipsAsync(query, ct)).Items;

    protected override Guid IdOf(AdminVipSummary row) => row.UserProfileId;
}
