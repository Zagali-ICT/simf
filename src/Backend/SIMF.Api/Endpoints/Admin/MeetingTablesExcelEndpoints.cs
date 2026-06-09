// Tests: SIMF.Api.Tests/MeetingTablesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.BusinessMeetings.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/meeting-tables/export</c> — the D-356 grid export for a
/// hall's meeting tables (SIMF-FDS-013). <b>Export only:</b> meeting tables are
/// defined / generated from the page's own modals, so there is no generic
/// import. Unlike a flat resource, the meeting-tables grid is <i>hall-scoped</i>
/// (<see cref="IBusinessMeetingService.ListTablesAsync"/> needs a hall id), so
/// the CP carries the selected hall through <c>GridQuery.Filters["hallId"]</c>;
/// the base owns the auth gate, the ids-or-query selection (capped) and the
/// workbook render. With no hall id in the query the export is empty (there is
/// no all-halls listing).
/// </summary>
public sealed class ExportMeetingTablesEndpoint(IBusinessMeetingService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<MeetingTableRow>(exporter)
{
    /// <summary>The <see cref="GridQuery.Filters"/> key the CP uses to carry the
    /// hall id (the meeting-tables grid is scoped to one hall at a time).</summary>
    public const string HallIdFilterKey = "hallId";

    protected override string RoutePath => "/admin/meeting-tables/export";
    protected override string Permission => PermissionCatalog.MeetingTables.Export;
    protected override string SheetName => "MeetingTables";
    protected override string FilePrefix => "simf-meeting-tables";

    protected override IReadOnlyList<GridExcelColumn<MeetingTableRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<MeetingTableRow>> _columns =
    [
        new("Code", row => row.Code),
        new("Row", row => row.RowLabel),
        new("Column", row => row.ColumnNumber),
        new("Capacity", row => row.Capacity),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<MeetingTableRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        if (!query.Filters.TryGetValue(HallIdFilterKey, out var raw)
            || !Guid.TryParse(raw, out var hallId))
        {
            return [];
        }

        return (await service.ListTablesAsync(hallId, query, ct)).Items;
    }

    protected override Guid IdOf(MeetingTableRow row) => row.Id;
}
