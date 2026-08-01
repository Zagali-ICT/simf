// Tests: SIMF.Api.Tests/RatingsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/ratings/export</c> — the grid export for submitted
/// rating responses. <b>Export only:</b> responses are owned by the attendees who
/// submit them, so the admin Ratings page is a read-only viewer. Columns mirror
/// the CP grid (type, target, overall, comment, answer count, active, created-at);
/// rows come from the same <see cref="IAdminRatingResponseService.ListResponsesAsync"/>
/// the admin list endpoint uses.
/// </summary>
public sealed class ExportRatingsEndpoint(IAdminRatingResponseService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminRatingResponseSummary>(exporter)
{
    protected override string RoutePath => "/admin/ratings/export";
    protected override string Permission => PermissionCatalog.Ratings.Export;
    protected override string SheetName => "Ratings";
    protected override string FilePrefix => "simf-ratings";

    protected override IReadOnlyList<GridExcelColumn<AdminRatingResponseSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminRatingResponseSummary>> _columns =
    [
        new("Type", row => row.RatingTypeName),
        new("Target", row => row.TargetId == Guid.Empty ? string.Empty : row.TargetId.ToString()),
        new("Overall", row => row.OverallStars),
        new("Comment", row => row.Comment),
        new("Answers", row => row.AnswerCount),
        new("AverageAnswerStars", row => row.AverageAnswerStars),
        new("IsActive", row => row.IsActive),
        new("CreatedAt", row => row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
    ];

    protected override async Task<IReadOnlyList<AdminRatingResponseSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListResponsesAsync(query, ct)).Responses.Items;

    protected override Guid IdOf(AdminRatingResponseSummary row) => row.Id;
}
