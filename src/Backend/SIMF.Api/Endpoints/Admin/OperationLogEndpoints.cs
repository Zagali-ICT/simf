// Tests: SIMF.Api.Tests/AdminOperationLogTests.cs, SIMF.Api.Tests/AdminOperationLogExportTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-134 Sprint A — read-only viewer over the audit OperationLog table.
/// Administrator-only; no rate-limit (read endpoints).
/// </summary>
public sealed class ListOperationLogEndpoint(IAdminOperationLogService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminOperationLogSummary>>>
{
    public override void Configure()
    {
        Post("/admin/operation-log/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OperationLog.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of OperationLog entries with optional filters. " +
            "Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminOperationLogSummary>>.Ok(page), ct);
    }
}

public sealed class GetOperationLogRequest
{
    public Guid Id { get; set; }
}

public sealed class GetOperationLogEndpoint(IAdminOperationLogService service)
    : Endpoint<GetOperationLogRequest, ApiResult<AdminOperationLogDetail>>
{
    public override void Configure()
    {
        Get("/admin/operation-log/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OperationLog.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one OperationLog entry (full detail). Requires Administrator role.");
    }

    public override async Task HandleAsync(
        GetOperationLogRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct);
        if (detail is null)
        {
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "The audit entry was not found.",
                "لم يتم العثور على سجل التدقيق.");
        }
        await Send.OkAsync(ApiResult<AdminOperationLogDetail>.Ok(detail), ct);
    }
}

/// <summary>
/// P1.6 — XLSX export of the (filtered) operation log. Binary response (the
/// workbook bytes), so it does not use the <c>ApiResult</c> envelope. Gated by
/// the dedicated <c>OperationLog.Export</c> permission, separate from View.
/// </summary>
public sealed class ExportOperationLogEndpoint(IAdminOperationLogService service)
    : Endpoint<GridQuery>
{
    public override void Configure()
    {
        Post("/admin/operation-log/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OperationLog.Export), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Export the filtered OperationLog as an XLSX workbook. " +
            "Requires the OperationLog.Export permission.");
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
            $"attachment; filename=\"simf-operation-log-{SimfClock.Now:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
