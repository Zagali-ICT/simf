// Tests: SIMF.Api.Tests/Gates/AdminGatesTests.cs
//        SIMF.Api.Tests/GateOperatorModelTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateGateRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminGateDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>Bind {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). This class used to re-declare
/// the contract's fields and the endpoint hand-copied them across; it omitted
/// <c>HallId</c>, so FastEndpoints dropped the hall the Control Panel's picker
/// sends and <c>UpdateAsync</c> — which assigns it unconditionally — wiped the
/// stored binding on every edit. Renaming a gate silently demoted a hall door to a
/// perimeter gate, and a perimeter gate never feeds hall attendance. Inheriting
/// makes the drop impossible rather than merely detectable.</summary>
public sealed class UpdateGateRequest : AdminUpdateGateRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateGateEndpoint(IAdminGateService service)
    : Endpoint<UpdateGateRequest, ApiResult<AdminGateDetail>>
{
    public override void Configure()
    {
        Put("/admin/gates/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateGateRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        // Pass the bound request straight through. No hand-copy, so no
        // field can be dropped from it.
        await Send.OkAsync(ApiResult<AdminGateDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeactivateGateRequest { public Guid Id { get; set; } }

public sealed class DeactivateGateEndpoint(IAdminGateService service)
    : Endpoint<DeactivateGateRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/gates/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeactivateGateRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(ListGateAssignmentsRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminGateAssignmentRow>>.Ok(
            await service.ListAssignmentsAsync(req.Id, ct)), ct);
}

/// <summary>The CP gate form's operator picker. Lists the accounts that
/// can actually work a gate (approved app accounts on an operational profile type),
/// searchable + paged. Gated on <c>Gates.Manage</c> like the rest of the gate admin
/// surface, so a gate manager no longer needs <c>Admins.View</c> to fill the picker.
/// </summary>
public sealed class ListGateOperatorCandidatesEndpoint(IAdminGateService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminGateOperatorCandidate>>>
{
    public override void Configure()
    {
        Post("/admin/gates/operator-candidates/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminGateOperatorCandidate>>.Ok(
            await service.ListOperatorCandidatesAsync(req, ct)), ct);
}

/// <summary>The gate form's allowed-profile-type + hall lookups under
/// <c>Gates.Manage</c>, so a Security-team gate manager stops seeing empty
/// dropdowns fed by the ProfileTypes.View / Halls.View admin lists.</summary>
public sealed class GetGateFormOptionsEndpoint(IAdminGateService service)
    : EndpointWithoutRequest<ApiResult<AdminGateFormOptions>>
{
    public override void Configure()
    {
        Get("/admin/gates/form-options");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminGateFormOptions>.Ok(
            await service.GetFormOptionsAsync(ct)), ct);
}

public sealed class GateScansReportEndpoint(IAdminGateService service)
    : Endpoint<AdminGateScanReportFilter, ApiResult<IReadOnlyList<AdminGateScanRow>>>
{
    public override void Configure()
    {
        Post("/admin/gates/reports/scans");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminGateScanReportFilter req, CancellationToken ct)
    {
        var bytes = await service.ExportScansXlsxAsync(req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            // Saudi clock, like every other export filename. UtcNow stamped a
            // download made at 01:00 Riyadh with the previous day's date.
            $"attachment; filename=\"gate-scans-{SimfClock.Now:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
