// Tests: SIMF.Api.Tests/AdminAttendeesTests.cs, SIMF.Api.Tests/AdminAttendeesExportTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-134 Sprint A — read-only attendee roster across Visitors + Others.
/// Administrator-only; no rate-limit (read endpoint).
/// </summary>
public sealed class ListAttendeesEndpoint(IAdminAttendeeService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAttendeeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/attendees/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendees.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of the attendee roster (Visitors + Others). " +
            "Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminAttendeeSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// P1.6 — XLSX export of the (filtered) attendee roster. Binary response (the
/// workbook bytes), so it does not use the <c>ApiResult</c> envelope. Gated by
/// the dedicated <c>Attendees.Export</c> permission, separate from View.
/// </summary>
public sealed class ExportAttendeesEndpoint(IAdminAttendeeService service)
    : Endpoint<GridQuery>
{
    public override void Configure()
    {
        Post("/admin/attendees/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendees.Export), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Export the filtered attendee roster as an XLSX workbook. " +
            "Requires the Attendees.Export permission.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var bytes = await service.ExportAsync(actorId, req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"simf-attendees-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
