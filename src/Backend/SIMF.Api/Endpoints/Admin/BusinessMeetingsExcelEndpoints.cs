// Tests: SIMF.Api.Tests/BusinessMeetingsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.BusinessMeetings.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/business-meetings/export</c> — the grid export for
/// the admin-arranged B2B/B2C business-meetings desk. <b>Export
/// only:</b> meetings are scheduled and cancelled through the page's bespoke
/// modals, so no generic import path is added. The list is not parent-scoped, so
/// this delegates straight to the same service the list endpoint uses
/// (<see cref="IBusinessMeetingService.ListMeetingsAsync"/>); the columns mirror
/// the CP grid (meeting type + status resolved to display text).
/// </summary>
public sealed class ExportBusinessMeetingsEndpoint(IBusinessMeetingService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<BusinessMeetingRow>(exporter)
{
    protected override string RoutePath => "/admin/business-meetings/export";
    protected override string Permission => PermissionCatalog.BusinessMeetings.Export;
    protected override string SheetName => "BusinessMeetings";
    protected override string FilePrefix => "simf-business-meetings";

    protected override IReadOnlyList<GridExcelColumn<BusinessMeetingRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<BusinessMeetingRow>> _columns =
    [
        new("Hall", row => row.HallName),
        new("Table", row => row.TableCode),
        new("Type", row => TypeText(row.MeetingType)),
        new("Start", row => row.Start),
        new("End", row => row.End),
        new("Parties", row => row.ParticipantCount),
        new("Status", row => StatusText(row.Status)),
    ];

    protected override async Task<IReadOnlyList<BusinessMeetingRow>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListMeetingsAsync(query, ct)).Items;

    protected override Guid IdOf(BusinessMeetingRow row) => row.Id;

    // Resolve the wire enums to display text for the workbook cells.
    private static string TypeText(BusinessMeetingType type) => type switch
    {
        BusinessMeetingType.B2C => "B2C",
        BusinessMeetingType.G2B => "G2B",
        _ => "B2B",
    };

    private static string StatusText(BusinessMeetingStatus status) => status switch
    {
        BusinessMeetingStatus.Cancelled => "Cancelled",
        _ => "Confirmed",
    };
}
