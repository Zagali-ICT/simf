// Tests: SIMF.Api.Tests/InterestTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/interests/list</c> — paged + filtered grid of
/// every interest. Administrator-only.
/// </summary>
public sealed class ListInterestsEndpoint(IInterestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminInterestSummary>>>
{
    public override void Configure()
    {
        Post("/admin/interests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Interests.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of interests. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAllAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminInterestSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/admin/interests/{id}</c> — one interest by id, for the
/// CP edit page. Administrator-only.
/// </summary>
public sealed class GetInterestRequest
{
    public Guid Id { get; set; }
}

public sealed class GetInterestEndpoint(IInterestService service)
    : Endpoint<GetInterestRequest, ApiResult<AdminInterestSummary>>
{
    public override void Configure()
    {
        Get("/admin/interests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Interests.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(GetInterestRequest req, CancellationToken ct)
    {
        var summary = await service.GetAsync(req.Id, ct);
        if (summary is null)
        {
            throw new ApiException(
                ErrorCodes.InterestNotFound, 404,
                "The interest was not found.",
                "لم يتم العثور على الاهتمام.");
        }
        await Send.OkAsync(ApiResult<AdminInterestSummary>.Ok(summary), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/interests</c> — creates a new interest.
/// Administrator-only; the name must be unique.
/// </summary>
public sealed class CreateInterestEndpoint(IInterestService service)
    : Endpoint<AdminCreateInterestRequest, ApiResult<AdminInterestSummary>>
{
    public override void Configure()
    {
        Post("/admin/interests");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Interests.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminCreateInterestRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var summary = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminInterestSummary>.Ok(summary), ct);
    }
}

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// several PUT endpoints — sessions, gates and profile types among them —
/// silently dropped a field. Passing the bound request straight through
/// makes that drop impossible.</summary>
public sealed class UpdateInterestRequest : AdminUpdateInterestRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateInterestEndpoint(IInterestService service)
    : Endpoint<UpdateInterestRequest, ApiResult<AdminInterestSummary>>
{
    public override void Configure()
    {
        Put("/admin/interests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Interests.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Update an interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        UpdateInterestRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var summary = await service.UpdateAsync(actorId, req.Id,
            req, ct);
        await Send.OkAsync(ApiResult<AdminInterestSummary>.Ok(summary), ct);
    }
}

/// <summary>
/// <c>DELETE /api/v1/admin/interests/{id}</c> — soft-deletes (deactivates)
/// an interest. Administrator-only; idempotent.
/// </summary>
public sealed class DeactivateInterestRequest
{
    public Guid Id { get; set; }
}

public sealed class DeactivateInterestEndpoint(IInterestService service)
    : Endpoint<DeactivateInterestRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/interests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Interests.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Deactivate (soft-delete) an interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        DeactivateInterestRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
