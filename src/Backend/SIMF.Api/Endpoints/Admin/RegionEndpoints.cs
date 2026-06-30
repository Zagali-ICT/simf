// Tests: SIMF.Api.Tests/RegionTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Regions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Regions;

namespace SIMF.Api.Endpoints.Admin;

// Region lookup module — admin CRUD + the public app read, all in one file.
// Validation lives in the service (mirrors AdminOrganisationService); endpoints
// just gate + bind + delegate.
//
// CreateRegionRequest / UpdateRegionRequest carry no route id, so the {id} PUT
// reads the route GUID via Route<Guid>("id") (Organisation pattern) rather than
// a derived route-merge type. GET/DELETE bind the {id} route to this small DTO.
public sealed class RegionByIdRequest
{
    /// <summary>The region's primary key, bound from the <c>{id}</c> route segment.</summary>
    public Guid Id { get; set; }
}

/// <summary><c>POST /admin/regions/list</c> — paged admin grid over
/// <c>Regions</c>. Mirrors ListOrganisationsEndpoint.</summary>
public sealed class ListRegionsEndpoint(IAdminRegionService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminRegionSummary>>>
{
    public override void Configure()
    {
        Post("/admin/regions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Regions.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminRegionSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

/// <summary><c>GET /admin/regions/{id}</c> — a single region's full detail.
/// Mirrors GetOrganisationEndpoint.</summary>
public sealed class GetRegionEndpoint(IAdminRegionService service)
    : Endpoint<RegionByIdRequest, ApiResult<AdminRegionDetail>>
{
    public override void Configure()
    {
        Get("/admin/regions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Regions.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(RegionByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.RegionNotFound, 404,
                "The region was not found.",
                "لم يتم العثور على المنطقة.");
        await Send.OkAsync(ApiResult<AdminRegionDetail>.Ok(detail), ct);
    }
}

/// <summary><c>POST /admin/regions</c> — create a new region.
/// Mirrors CreateOrganisationEndpoint.</summary>
public sealed class CreateRegionEndpoint(IAdminRegionService service)
    : Endpoint<CreateRegionRequest, ApiResult<AdminRegionDetail>>
{
    public override void Configure()
    {
        Post("/admin/regions");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Regions.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateRegionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRegionDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary><c>PUT /admin/regions/{id}</c> — update a region. Reads the route
/// GUID via <c>Route&lt;Guid&gt;("id")</c>; the contract request carries no id.
/// Mirrors UpdateOrganisationEndpoint.</summary>
public sealed class UpdateRegionEndpoint(IAdminRegionService service)
    : Endpoint<UpdateRegionRequest, ApiResult<AdminRegionDetail>>
{
    public override void Configure()
    {
        Put("/admin/regions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Regions.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateRegionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRegionDetail>.Ok(
            await service.UpdateAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

/// <summary><c>DELETE /admin/regions/{id}</c> — soft-deactivate a region.
/// Mirrors DeactivateOrganisationEndpoint.</summary>
public sealed class DeactivateRegionEndpoint(IAdminRegionService service)
    : Endpoint<RegionByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/regions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Regions.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RegionByIdRequest req, CancellationToken ct)
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

/// <summary>
/// <c>GET /app/regions</c> — the public region picker list for the app. Returns
/// the active regions ordered for the picker.
///
/// <para>Auth: the Configure() shape mirrors OrganisationPickerSearchEndpoint
/// (<c>Tags</c> + the <c>auth</c> rate-limit bucket, no explicit
/// <c>Policies(...)</c>), so the caller must be signed in but is not
/// admin-gated and not approval-gated. <b>Not</b> <c>AllowAnonymous</c>.</para>
/// </summary>
public sealed class RegionListEndpoint(IPublicRegionService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<RegionPickerItem>>>
{
    public override void Configure()
    {
        Get("/app/regions");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "List the active regions for the picker (requires sign-in).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = await service.GetActiveRegionsAsync(ct);
        await Send.OkAsync(
            ApiResult<IReadOnlyList<RegionPickerItem>>.Ok(items), ct);
    }
}
