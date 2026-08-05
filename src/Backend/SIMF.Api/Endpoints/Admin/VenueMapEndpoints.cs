// Tests: SIMF.Api.Tests/VenueMapTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Venue.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>P2.5 — D-230 (FR-605, FDS-006 §5.3): 2D venue-map node CRUD, gated
/// by VenueMap.*. The public read lives in VenueMapPublicEndpoints.</summary>
public sealed class ListVenueMapNodesEndpoint(IVenueMapService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminVenueMapNodeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/venue-map/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.VenueMap.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminVenueMapNodeSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetVenueMapNodeRequest { public Guid Id { get; set; } }

public sealed class GetVenueMapNodeEndpoint(IVenueMapService service)
    : Endpoint<GetVenueMapNodeRequest, ApiResult<AdminVenueMapNodeDetail>>
{
    public override void Configure()
    {
        Get("/admin/venue-map/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.VenueMap.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetVenueMapNodeRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.VenueMapNodeNotFound, 404,
                "The venue-map node was not found.",
                "لم يتم العثور على عقدة الخريطة.");
        await Send.OkAsync(ApiResult<AdminVenueMapNodeDetail>.Ok(detail), ct);
    }
}

public sealed class CreateVenueMapNodeEndpoint(IVenueMapService service)
    : Endpoint<AdminCreateVenueMapNodeRequest, ApiResult<AdminVenueMapNodeDetail>>
{
    public override void Configure()
    {
        Post("/admin/venue-map");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.VenueMap.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateVenueMapNodeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminVenueMapNodeDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateVenueMapNodeEndpoint(IVenueMapService service)
    : Endpoint<AdminUpdateVenueMapNodeRequest, ApiResult<AdminVenueMapNodeDetail>>
{
    public override void Configure()
    {
        Put("/admin/venue-map/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.VenueMap.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminUpdateVenueMapNodeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("id");
        await Send.OkAsync(ApiResult<AdminVenueMapNodeDetail>.Ok(
            await service.UpdateAsync(actorId, id, req, ct)), ct);
    }
}

public sealed class DeleteVenueMapNodeRequest { public Guid Id { get; set; } }

public sealed class DeleteVenueMapNodeEndpoint(IVenueMapService service)
    : Endpoint<DeleteVenueMapNodeRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/venue-map/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.VenueMap.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteVenueMapNodeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
