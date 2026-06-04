// Tests: SIMF.Api.Tests/ExhibitorsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Api.Endpoints.Exhibitors;

/// <summary>D-199 #3 — admin exhibitor list. Mirrors ListSponsorsEndpoint.</summary>
public sealed class ListExhibitorsEndpoint(IAdminExhibitorService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminExhibitorSummary>>>
{
    public override void Configure()
    {
        Post("/admin/exhibitors/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminExhibitorSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetExhibitorRoute { public Guid Id { get; set; } }

public sealed class GetExhibitorEndpoint(IAdminExhibitorService service)
    : Endpoint<GetExhibitorRoute, ApiResult<AdminExhibitorDetail>>
{
    public override void Configure()
    {
        Get("/admin/exhibitors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetExhibitorRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        await Send.OkAsync(ApiResult<AdminExhibitorDetail>.Ok(detail), ct);
    }
}

public sealed class CreateExhibitorEndpoint(IAdminExhibitorService service)
    : Endpoint<CreateExhibitorRequest, ApiResult<AdminExhibitorDetail>>
{
    public override void Configure()
    {
        Post("/admin/exhibitors");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateExhibitorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminExhibitorDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateExhibitorRoute : UpdateExhibitorRequest { public Guid Id { get; set; } }

public sealed class UpdateExhibitorEndpoint(IAdminExhibitorService service)
    : Endpoint<UpdateExhibitorRoute, ApiResult<AdminExhibitorDetail>>
{
    public override void Configure()
    {
        Put("/admin/exhibitors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateExhibitorRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminExhibitorDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteExhibitorRoute { public Guid Id { get; set; } }

public sealed class DeleteExhibitorEndpoint(IAdminExhibitorService service)
    : Endpoint<DeleteExhibitorRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/exhibitors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeleteExhibitorRoute req, CancellationToken ct)
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

// -- Exhibitor account provisioning --

public sealed class ListExhibitorAccountsRoute { public Guid Id { get; set; } }

public sealed class ListExhibitorAccountsEndpoint(IAdminExhibitorService service)
    : Endpoint<ListExhibitorAccountsRoute, ApiResult<IReadOnlyList<ExhibitorAccountSummary>>>
{
    public override void Configure()
    {
        Get("/admin/exhibitors/{id:guid}/accounts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ListExhibitorAccountsRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<ExhibitorAccountSummary>>.Ok(
            await service.ListAccountsAsync(req.Id, ct)), ct);
}

public sealed class ProvisionExhibitorAccountRoute : ProvisionExhibitorAccountRequest
{
    public Guid Id { get; set; }
}

public sealed class ProvisionExhibitorAccountEndpoint(IAdminExhibitorService service)
    : Endpoint<ProvisionExhibitorAccountRoute, ApiResult<ExhibitorAccountSummary>>
{
    public override void Configure()
    {
        Post("/admin/exhibitors/{id:guid}/accounts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Exhibitors.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ProvisionExhibitorAccountRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<ExhibitorAccountSummary>.Ok(
            await service.ProvisionAccountAsync(actorId, req.Id, req, ct)), ct);
    }
}
