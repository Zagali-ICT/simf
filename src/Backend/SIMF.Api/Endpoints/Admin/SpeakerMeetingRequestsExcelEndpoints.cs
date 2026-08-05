// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Api.RequestContext;
using SIMF.Application.Excel;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/speaker-meeting-requests/export</c> — the D-356 grid
/// export for the admin speaker-meeting-requests queue (D-269). <b>Export only:</b>
/// these requests are created from the app and responded to from the CP modal, so
/// there is no generic import path. The columns mirror the CP grid's visible
/// columns; the requester email is deliberately NOT exported (it is per-record
/// PII surfaced only via the audited detail endpoint, the D-185 pattern). The row
/// list reuses the same service the list endpoint calls so filters/sorts behave
/// identically. The actor is resolved from the access token for the service's
/// audit-log entry.
/// </summary>
public sealed class ExportSpeakerMeetingRequestsEndpoint(ISpeakerMeetingRequestService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSpeakerMeetingRequestRow>(exporter)
{
    protected override string RoutePath => "/admin/speaker-meeting-requests/export";
    protected override string Permission => PermissionCatalog.SpeakerMeetingRequests.Export;
    protected override string SheetName => "SpeakerMeetingRequests";
    protected override string FilePrefix => "simf-speaker-meeting-requests";

    protected override IReadOnlyList<GridExcelColumn<AdminSpeakerMeetingRequestRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSpeakerMeetingRequestRow>> _columns =
    [
        new("Speaker", row => row.SpeakerName),
        new("Requester", row => row.RequesterName),
        new("Subject", row => row.Subject),
        new("Status", row => row.Status.ToString()),
        new("CreatedAt", row => row.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
        new("RespondedAt", row => row.RespondedAt is null
            ? string.Empty
            : row.RespondedAt.Value.ToString("yyyy-MM-dd HH:mm")),
        // OA-D5 — the hall check-in stamps. They were written by the check-in action
        // (POST /admin/speaker-meeting-requests/{id}/check-in) but appeared in no
        // report, so "who actually turned up" was unanswerable off-screen.
        new("CheckedInAt", row => row.CheckedInAt is null
            ? string.Empty
            : row.CheckedInAt.Value.ToString("yyyy-MM-dd HH:mm")),
        new("CheckedInBy", row => row.CheckedInByName ?? string.Empty),
    ];

    protected override async Task<IReadOnlyList<AdminSpeakerMeetingRequestRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        var actorId = User.ActorId();
        return (await service.ListAllAsync(actorId, query, ct)).Items;
    }

    protected override Guid IdOf(AdminSpeakerMeetingRequestRow row) => row.Id;
}
