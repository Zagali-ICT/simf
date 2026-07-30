// Tests: SIMF.Api.Tests/ReportingTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Reporting.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Reporting;

namespace SIMF.Api.Endpoints.Reporting;

/// <summary>
/// The reporting module's admin API. Every endpoint is read-only, POSTs its
/// query (a report request carries a nested grid query plus a date range, which
/// does not belong in a URL), and is gated by its own per-report permission plus
/// an approved account.
///
/// <para>Export is gated separately from viewing: the export permission is what
/// authorises taking the data off the premises as a file.</para>
///
/// <para>Routes are RELATIVE — the FastEndpoints <c>RoutePrefix</c> supplies
/// <c>api/v1</c>.</para>
/// </summary>
public sealed class ListAttendanceReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<AttendanceReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/attendance/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Attendance),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary =
            "One page of the attendance report: per-session distinct attendees " +
            "and how many are still inside, over a Saudi date range.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<AttendanceReportRow>>.Ok(
            await service.GetAttendanceAsync(req, ct)), ct);
}

public sealed class ExportAttendanceReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/attendance/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary = "Export the filtered attendance report as XLSX.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportAttendanceAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "attendance");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListRegistrationsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<RegistrationReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/registrations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Registrations),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary =
            "One page of the registrations report: attendee accounts created " +
            "inside a Saudi date range, with their profile type and state.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<RegistrationReportRow>>.Ok(
            await service.GetRegistrationsAsync(req, ct)), ct);
}

public sealed class ExportRegistrationsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/registrations/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary = "Export the filtered registrations report as XLSX.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportRegistrationsAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "registrations");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListGateActivityReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<GateActivityReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/gates/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Gates),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary =
            "One page of the gate-activity report: recorded scans, allowed and " +
            "denied, over a Saudi date range.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<GateActivityReportRow>>.Ok(
            await service.GetGateActivityAsync(req, ct)), ct);
}

public sealed class ExportGateActivityReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/gates/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
        Summary(s => s.Summary = "Export the filtered gate-activity report as XLSX.");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportGateActivityAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "gate-activity");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}
