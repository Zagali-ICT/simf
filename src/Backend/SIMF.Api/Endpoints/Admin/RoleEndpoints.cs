// Tests: SIMF.Api.Tests/AdminRolesTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-134 Sprint A — admin CRUD over the SimfRole table. Built on the
/// existing Identity schema; this file follows the InterestEndpoints
/// pattern (one endpoint per file-section, FastEndpoints conventions,
/// ApiResult envelope, Administrator-only).
/// </summary>
public sealed class ListRolesEndpoint(IAdminRoleService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminRoleSummary>>>
{
    public override void Configure()
    {
        Post("/admin/roles/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of roles. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAllAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminRoleSummary>>.Ok(page), ct);
    }
}

public sealed class GetRoleRequest
{
    public Guid Id { get; set; }
}

public sealed class GetRoleEndpoint(IAdminRoleService service)
    : Endpoint<GetRoleRequest, ApiResult<AdminRoleSummary>>
{
    public override void Configure()
    {
        Get("/admin/roles/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one role. Requires Administrator role.");
    }

    public override async Task HandleAsync(GetRoleRequest req, CancellationToken ct)
    {
        var summary = await service.GetAsync(req.Id, ct);
        if (summary is null)
        {
            throw new ApiException(
                ErrorCodes.RoleNotFound, 404,
                "The role was not found.",
                "لم يتم العثور على الدور.");
        }
        await Send.OkAsync(ApiResult<AdminRoleSummary>.Ok(summary), ct);
    }
}

public sealed class CreateRoleEndpoint(IAdminRoleService service)
    : Endpoint<AdminCreateRoleRequest, ApiResult<AdminRoleSummary>>
{
    public override void Configure()
    {
        Post("/admin/roles");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new custom role. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminCreateRoleRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminRoleSummary>.Ok(summary), ct);
    }
}

public sealed class UpdateRoleRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateRoleEndpoint(IAdminRoleService service)
    : Endpoint<UpdateRoleRequest, ApiResult<AdminRoleSummary>>
{
    public override void Configure()
    {
        Put("/admin/roles/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Rename a custom role. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        UpdateRoleRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.UpdateAsync(actorId, req.Id,
            new AdminUpdateRoleRequest { Name = req.Name }, ct);
        await Send.OkAsync(ApiResult<AdminRoleSummary>.Ok(summary), ct);
    }
}

public sealed class DeleteRoleRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteRoleEndpoint(IAdminRoleService service)
    : Endpoint<DeleteRoleRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/roles/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Delete a custom role. Requires Administrator role. " +
            "Baseline roles cannot be deleted, and roles still held by " +
            "any user are refused with RoleInUse (409).");
    }

    public override async Task HandleAsync(
        DeleteRoleRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeleteAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
