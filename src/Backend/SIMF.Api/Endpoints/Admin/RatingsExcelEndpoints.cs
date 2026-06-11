// Tests: SIMF.Api.Tests/RatingsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/ratings/export</c> — the D-356 grid export for the
/// attendee forum ratings (Mockup screen 40 "Rate the Forum"). <b>Export only:</b>
/// ratings are owned by the attendees who submit them, so the admin Ratings page
/// is a read-only viewer — no generic import endpoint is added here. The columns
/// mirror the CP grid's visible columns (stars, comment, active, created-at);
/// rows come from the same <see cref="IRatingService.ListAllAsync"/> the admin
/// list endpoint uses.
/// </summary>
public sealed class ExportRatingsEndpoint(IRatingService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminRatingSummary>(exporter)
{
    protected override string RoutePath => "/admin/ratings/export";
    protected override string Permission => PermissionCatalog.Ratings.Export;
    protected override string SheetName => "Ratings";
    protected override string FilePrefix => "simf-ratings";

    protected override IReadOnlyList<GridExcelColumn<AdminRatingSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminRatingSummary>> _columns =
    [
        new("Stars", row => row.Stars),
        new("Comment", row => row.Comment),
        new("IsActive", row => row.IsActive),
        new("CreatedAt", row => row.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
    ];

    protected override async Task<IReadOnlyList<AdminRatingSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Ratings.Items;

    protected override Guid IdOf(AdminRatingSummary row) => row.Id;
}
