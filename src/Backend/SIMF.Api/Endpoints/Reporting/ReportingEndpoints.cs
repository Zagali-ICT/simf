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

public sealed class ListSessionsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<SessionsReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/sessions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Sessions),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<SessionsReportRow>>.Ok(
            await service.GetSessionsAsync(req, ct)), ct);
}

public sealed class ExportSessionsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/sessions/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportSessionsAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "sessions");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListRatingsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<RatingsReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/ratings/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Ratings),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<RatingsReportRow>>.Ok(
            await service.GetRatingsAsync(req, ct)), ct);
}

public sealed class ExportRatingsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/ratings/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportRatingsAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "ratings");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListPartnersReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<PartnersReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/partners/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Partners),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<PartnersReportRow>>.Ok(
            await service.GetPartnersAsync(req, ct)), ct);
}

public sealed class ExportPartnersReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/partners/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportPartnersAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "partners");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListMeetingsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<MeetingsReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/meetings/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Meetings),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<MeetingsReportRow>>.Ok(
            await service.GetMeetingsAsync(req, ct)), ct);
}

public sealed class ExportMeetingsReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/meetings/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportMeetingsAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "meetings");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}

public sealed class ListEngagementReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery, ApiResult<ReportPage<EngagementReportRow>>>
{
    public override void Configure()
    {
        Post("/admin/reports/engagement/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Engagement),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ReportPage<EngagementReportRow>>.Ok(
            await service.GetEngagementAsync(req, ct)), ct);
}

public sealed class ExportEngagementReportEndpoint(IReportingService service)
    : Endpoint<ReportQuery>
{
    public override void Configure()
    {
        Post("/admin/reports/engagement/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Reports.Export),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Reports");
    }

    public override async Task HandleAsync(ReportQuery req, CancellationToken ct)
    {
        var bytes = await service.ExportEngagementAsync(req, ct);
        ReportDownload.SetAttachmentHeader(HttpContext, "engagement");
        await Send.BytesAsync(
            bytes, contentType: ReportDownload.XlsxContentType, cancellation: ct);
    }
}
