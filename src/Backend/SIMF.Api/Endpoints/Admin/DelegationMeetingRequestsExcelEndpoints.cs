// Tests: SIMF.Api.Tests/MeetingCheckInExportTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Api.RequestContext;
using SIMF.Application.Excel;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/delegation-meeting-requests/export</c> — the grid
/// export for the delegation (G2G) meeting-request desk. The speaker desk has
/// long had one; this desk had a check-in route but no export at
/// all, so the <c>CheckedInAt</c> / <c>CheckedInByUserId</c> stamps it writes were
/// reachable from no report.
///
/// <para><b>Export only</b> — these requests are created from the app and decided
/// in the CP modal, so there is no generic import path. The columns mirror the CP
/// grid's visible columns; the requester email is deliberately NOT exported — it
/// is per-record PII, surfaced only via the audited detail
/// endpoint. Gated on its own <see cref="PermissionCatalog.DelegationMeetings.Export"/>
/// code rather than <c>View</c>, for the same reason as every other export gate:
/// taking a spreadsheet of meetings off the premises is a bigger act than reading
/// a page of them on screen.</para>
/// </summary>
public sealed class ExportDelegationMeetingRequestsEndpoint(
    IDelegationMeetingRequestService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminDelegationMeetingRequestRow>(exporter)
{
    protected override string RoutePath => "/admin/delegation-meeting-requests/export";
    protected override string Permission => PermissionCatalog.DelegationMeetings.Export;
    protected override string SheetName => "DelegationMeetingRequests";
    protected override string FilePrefix => "simf-delegation-meeting-requests";

    protected override IReadOnlyList<GridExcelColumn<AdminDelegationMeetingRequestRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminDelegationMeetingRequestRow>> _columns =
    [
        new("RequestingCountry", row => row.RequestingCountry),
        new("TargetCountry", row => row.TargetCountry),
        new("Attendees", row => row.AttendeeCount.ToString()),
        new("Subject", row => row.Subject),
        new("Status", row => row.Status.ToString()),
        new("SlotStart", row => Stamp(row.SlotStart)),
        new("CreatedAt", row => row.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
        new("RespondedAt", row => Stamp(row.RespondedAt)),
        // The hall check-in stamps, the whole point of this export.
        new("CheckedInAt", row => Stamp(row.CheckedInAt)),
        new("CheckedInBy", row => row.CheckedInByName ?? string.Empty),
    ];

    protected override async Task<IReadOnlyList<AdminDelegationMeetingRequestRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        var actorId = User.ActorId();
        return (await service.ListAllAsync(actorId, query, ct)).Items;
    }

    protected override Guid IdOf(AdminDelegationMeetingRequestRow row) => row.Id;

    /// <summary>Renders an optional instant in the same shape every other grid
    /// export uses, or an empty cell when it is not set.</summary>
    private static string Stamp(DateTime? value) =>
        value is null
            ? string.Empty
            : value.Value.ToString("yyyy-MM-dd HH:mm");
}
