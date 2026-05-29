// Tests: SIMF.Api.Tests/Gates/AdminGatesTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

public sealed class ListGatesEndpoint(IAdminGateService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminGateSummary>>>
{
    public override void Configure()
    {
        Post("/admin/gates/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminGateSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetGateRequest { public Guid Id { get; set; } }

public sealed class GetGateEndpoint(IAdminGateService service)
    : Endpoint<GetGateRequest, ApiResult<AdminGateDetail>>
{
    public override void Configure()
    {
        Get("/admin/gates/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetGateRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.GateNotFound, 404,
                "The gate was not found.",
                "لم يتم العثور على البوابة.");
        await Send.OkAsync(ApiResult<AdminGateDetail>.Ok(detail), ct);
    }
}

public sealed class CreateGateEndpoint(IAdminGateService service)
    : Endpoint<AdminCreateGateRequest, ApiResult<AdminGateDetail>>
{
    public override void Configure()
    {
        Post("/admin/gates");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateGateRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminGateDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateGateRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;
    public bool IsActive { get; set; } = true;
    public List<Guid> AllowedProfileTypeIds { get; set; } = new();
    public List<Guid> AssignedOperatorUserIds { get; set; } = new();
}

public sealed class UpdateGateEndpoint(IAdminGateService service)
    : Endpoint<UpdateGateRequest, ApiResult<AdminGateDetail>>
{
    public override void Configure()
    {
        Put("/admin/gates/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateGateRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminGateDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, new AdminUpdateGateRequest
            {
                Code = req.Code, Name = req.Name, NameArabic = req.NameArabic,
                Description = req.Description, DescriptionArabic = req.DescriptionArabic,
                DirectionMode = req.DirectionMode,
                IsActive = req.IsActive,
                AllowedProfileTypeIds = req.AllowedProfileTypeIds,
                AssignedOperatorUserIds = req.AssignedOperatorUserIds,
            }, ct)), ct);
    }
}

public sealed class DeactivateGateRequest { public Guid Id { get; set; } }

public sealed class DeactivateGateEndpoint(IAdminGateService service)
    : Endpoint<DeactivateGateRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/gates/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeactivateGateRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class ListGateAssignmentsRequest { public Guid Id { get; set; } }

public sealed class ListGateAssignmentsEndpoint(IAdminGateService service)
    : Endpoint<ListGateAssignmentsRequest, ApiResult<IReadOnlyList<AdminGateAssignmentRow>>>
{
    public override void Configure()
    {
        Get("/admin/gates/{id:guid}/assignments");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(ListGateAssignmentsRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminGateAssignmentRow>>.Ok(
            await service.ListAssignmentsAsync(req.Id, ct)), ct);
}

public sealed class GateScansReportEndpoint(IAdminGateService service)
    : Endpoint<AdminGateScanReportFilter, ApiResult<IReadOnlyList<AdminGateScanRow>>>
{
    public override void Configure()
    {
        Post("/admin/gates/reports/scans");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminGateScanReportFilter req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminGateScanRow>>.Ok(
            await service.ListScansAsync(req, ct)), ct);
}

public sealed class GateCurrentlyInsideEndpoint(IAdminGateService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>>
{
    public override void Configure()
    {
        Get("/admin/gates/reports/currently-inside");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>.Ok(
            await service.ListCurrentlyInsideAsync(ct)), ct);
}

public sealed class GateScansXlsxEndpoint(IAdminGateService service)
    : Endpoint<AdminGateScanReportFilter>
{
    public override void Configure()
    {
        Post("/admin/gates/reports/scans.xlsx");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount),
                 nameof(AuthorizationPolicies.GatesManage));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminGateScanReportFilter req, CancellationToken ct)
    {
        var bytes = await service.ExportScansXlsxAsync(req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"gate-scans-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
