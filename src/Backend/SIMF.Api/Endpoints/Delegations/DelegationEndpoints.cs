// Tests: SIMF.Api.Tests/DelegationsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Delegations;

namespace SIMF.Api.Endpoints.Delegations;

/// <summary>D-174 (gap doc G11, Mockup page 21) — public anonymous
/// list of active delegations.</summary>
public sealed class ListPublicDelegationsEndpoint(IPublicDelegationService service)
    : EndpointWithoutRequest<ApiResult<PublicDelegations>>
{
    public override void Configure()
    {
        Get("/delegations");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicDelegations>.Ok(
            await service.ListAsync(ct)), ct);
}

// -- Admin delegation CRUD --

public sealed class ListAdminDelegationsEndpoint(IAdminDelegationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminDelegationSummary>>>
{
    public override void Configure()
    {
        Post("/admin/delegations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Delegations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminDelegationSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetDelegationRoute { public Guid Id { get; set; } }

public sealed class GetDelegationEndpoint(IAdminDelegationService service)
    : Endpoint<GetDelegationRoute, ApiResult<AdminDelegationSummary>>
{
    public override void Configure()
    {
        Get("/admin/delegations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Delegations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetDelegationRoute req, CancellationToken ct)
    {
        var summary = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.DelegationNotFound, 404,
                "Delegation not found.",
                "لم يتم العثور على الوفد.");
        await Send.OkAsync(ApiResult<AdminDelegationSummary>.Ok(summary), ct);
    }
}

public sealed class CreateDelegationEndpoint(IAdminDelegationService service)
    : Endpoint<CreateDelegationRequest, ApiResult<AdminDelegationSummary>>
{
    public override void Configure()
    {
        Post("/admin/delegations");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Delegations.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CreateDelegationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationSummary>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateDelegationRoute : UpdateDelegationRequest { public Guid Id { get; set; } }

public sealed class UpdateDelegationEndpoint(IAdminDelegationService service)
    : Endpoint<UpdateDelegationRoute, ApiResult<AdminDelegationSummary>>
{
    public override void Configure()
    {
        Put("/admin/delegations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Delegations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateDelegationRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationSummary>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteDelegationRoute { public Guid Id { get; set; } }

public sealed class DeleteDelegationEndpoint(IAdminDelegationService service)
    : Endpoint<DeleteDelegationRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/delegations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Delegations.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteDelegationRoute req, CancellationToken ct)
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
