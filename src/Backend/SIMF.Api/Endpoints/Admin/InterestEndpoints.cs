// Tests: SIMF.Api.Tests/InterestTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/interests/list</c> — paged + filtered grid of
/// every interest (P9 — D-050). Administrator-only.
/// </summary>
public sealed class ListInterestsEndpoint(IInterestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminInterestSummary>>>
{
    public override void Configure()
    {
        Post("/admin/interests/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
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
/// CP edit page (P9 — D-050). Administrator-only.
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
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
/// <c>POST /api/v1/admin/interests</c> — creates a new interest (P9 —
/// D-050). Administrator-only; the name must be unique.
/// </summary>
public sealed class CreateInterestEndpoint(IInterestService service)
    : Endpoint<AdminCreateInterestRequest, ApiResult<AdminInterestSummary>>
{
    public override void Configure()
    {
        Post("/admin/interests");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminCreateInterestRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminInterestSummary>.Ok(summary), ct);
    }
}

/// <summary>
/// <c>PUT /api/v1/admin/interests/{id}</c> — updates an interest (P9 —
/// D-050). Administrator-only. Route-id + body shape — FastEndpoints
/// merges them.
/// </summary>
public sealed class UpdateInterestRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateInterestEndpoint(IInterestService service)
    : Endpoint<UpdateInterestRequest, ApiResult<AdminInterestSummary>>
{
    public override void Configure()
    {
        Put("/admin/interests/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Update an interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        UpdateInterestRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.UpdateAsync(actorId, req.Id,
            new AdminUpdateInterestRequest
            {
                Name = req.Name,
                NameArabic = req.NameArabic,
                DisplayOrder = req.DisplayOrder,
                IsActive = req.IsActive,
            }, ct);
        await Send.OkAsync(ApiResult<AdminInterestSummary>.Ok(summary), ct);
    }
}

/// <summary>
/// <c>DELETE /api/v1/admin/interests/{id}</c> — soft-deletes (deactivates)
/// an interest (P9 — D-050). Administrator-only; idempotent.
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Deactivate (soft-delete) an interest. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        DeactivateInterestRequest req, CancellationToken ct)
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
