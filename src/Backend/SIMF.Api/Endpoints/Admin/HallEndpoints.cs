// Tests: SIMF.Api.Tests/AdminHallsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

public sealed class ListHallsEndpoint(IAdminHallService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminHallSummary>>>
{
    public override void Configure()
    {
        Post("/admin/halls/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        await Send.OkAsync(ApiResult<GridPage<AdminHallSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
    }
}

public sealed class GetHallRequest { public Guid Id { get; set; } }

public sealed class GetHallEndpoint(IAdminHallService service)
    : Endpoint<GetHallRequest, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Get("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetHallRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.",
                "لم يتم العثور على القاعة.");
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(detail), ct);
    }
}

public sealed class CreateHallEndpoint(IAdminHallService service)
    : Endpoint<AdminCreateHallRequest, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Post("/admin/halls");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateHallRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

// D-505 — bind {id} + body via a derived route that INHERITS the contract
// (mirrors UpdateExhibitorRoute / UpdateSponsorRoute). The old inline
// UpdateHallRequest omitted the GPS geofence fields, so FastEndpoints dropped
// the geofence the CP form sends and UpdateAsync wiped the stored geofence on
// every edit. Passing the bound req straight through makes the drop impossible.
public sealed class UpdateHallRoute : AdminUpdateHallRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateHallEndpoint(IAdminHallService service)
    : Endpoint<UpdateHallRoute, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Put("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateHallRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeactivateHallRequest { public Guid Id { get; set; } }

public sealed class DeactivateHallEndpoint(IAdminHallService service)
    : Endpoint<DeactivateHallRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeactivateHallRequest req, CancellationToken ct)
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
