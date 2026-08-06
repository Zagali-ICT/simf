// Tests: SIMF.Api.Tests/AdminThemeUpdateTests.cs
// Tests: SIMF.Api.Tests/ThemesExcelTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD over <c>Themes</c>
/// (SIMF-FDS-004 §5.1). Mirrors the InterestEndpoints shape.</summary>
public sealed class ListThemesEndpoint(IAdminThemeService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminThemeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/themes/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of programme themes. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAllAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminThemeSummary>>.Ok(page), ct);
    }
}

public sealed class GetThemeRequest { public Guid Id { get; set; } }

public sealed class GetThemeEndpoint(IAdminThemeService service)
    : Endpoint<GetThemeRequest, ApiResult<AdminThemeDetail>>
{
    public override void Configure()
    {
        Get("/admin/themes/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetThemeRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct);
        if (detail is null)
        {
            throw new ApiException(
                ErrorCodes.ThemeNotFound, 404,
                "The theme was not found.",
                "لم يتم العثور على المحور.");
        }
        await Send.OkAsync(ApiResult<AdminThemeDetail>.Ok(detail), ct);
    }
}

public sealed class CreateThemeEndpoint(IAdminThemeService service)
    : Endpoint<AdminCreateThemeRequest, ApiResult<AdminThemeDetail>>
{
    public override void Configure()
    {
        Post("/admin/themes");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateThemeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var detail = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminThemeDetail>.Ok(detail), ct);
    }
}

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract, per D-505 (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// D-842 (sessions), D-843 (gates, profile types) and the four before them
/// silently dropped a field on PUT. Passing the bound request straight through
/// makes that drop impossible.</summary>
public sealed class UpdateThemeRequest : AdminUpdateThemeRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateThemeEndpoint(IAdminThemeService service)
    : Endpoint<UpdateThemeRequest, ApiResult<AdminThemeDetail>>
{
    public override void Configure()
    {
        Put("/admin/themes/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateThemeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var detail = await service.UpdateAsync(actorId, req.Id,
            req, ct);
        await Send.OkAsync(ApiResult<AdminThemeDetail>.Ok(detail), ct);
    }
}

public sealed class DeactivateThemeRequest { public Guid Id { get; set; } }

public sealed class DeactivateThemeEndpoint(IAdminThemeService service)
    : Endpoint<DeactivateThemeRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/themes/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateThemeRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
