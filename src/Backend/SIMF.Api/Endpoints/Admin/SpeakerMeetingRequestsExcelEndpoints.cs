// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsExcelTests.cs
using System.Security.Claims;
using SIMF.Api.Endpoints.Admin.Grid;
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
        new("CreatedAt", row => row.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'")),
        new("RespondedAt", row => row.RespondedAt is null
            ? string.Empty
            : row.RespondedAt.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'")),
    ];

    protected override async Task<IReadOnlyList<AdminSpeakerMeetingRequestRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return [];
        }
        return (await service.ListAllAsync(actorId, query, ct)).Items;
    }

    protected override Guid IdOf(AdminSpeakerMeetingRequestRow row) => row.Id;
}
