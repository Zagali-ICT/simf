// Tests: SIMF.Api.Tests/AdminUserRolesTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Issue-1 — the RBAC roles an existing admin user holds. Drives the
/// Control Panel's user-roles editor (replaces the old stubbed Edit dialog).</summary>
public sealed class GetUserRolesRequest
{
    public Guid Id { get; set; }
}

public sealed class GetUserRolesEndpoint(IAdminUserProvisioningService service)
    : Endpoint<GetUserRolesRequest, ApiResult<AdminUserRolesResponse>>
{
    public override void Configure()
    {
        Get("/admin/admins/{id:guid}/roles");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary = "Return the roles an admin user currently holds.");
    }

    public override async Task HandleAsync(GetUserRolesRequest req, CancellationToken ct)
    {
        var response = await service.GetUserRolesAsync(req.Id, ct);
        await Send.OkAsync(ApiResult<AdminUserRolesResponse>.Ok(response), ct);
    }
}

public sealed class SetUserRolesRouteRequest
{
    public Guid Id { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>Issue-1 — replaces an admin user's RBAC roles. Gated by
/// <c>Admins.AssignRoles</c>.</summary>
public sealed class SetUserRolesEndpoint(IAdminUserProvisioningService service)
    : Endpoint<SetUserRolesRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/admin/admins/{id:guid}/roles");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.AssignRoles), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Set the roles an admin user holds. Requires Admins.AssignRoles.");
    }

    public override async Task HandleAsync(SetUserRolesRouteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.SetUserRolesAsync(actorId, req.Id, req.Roles, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
