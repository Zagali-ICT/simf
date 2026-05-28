// Tests: SIMF.Api.Tests/AdminThemesTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-134 Sprint B — admin CRUD over <c>Themes</c>
/// (SIMF-FDS-004 §5.1). Mirrors the InterestEndpoints shape.</summary>
public sealed class ListThemesEndpoint(IAdminThemeService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminThemeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/themes/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateThemeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var detail = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminThemeDetail>.Ok(detail), ct);
    }
}

public sealed class UpdateThemeRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public int DisplayOrder { get; set; }
    public string PageColor { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateThemeEndpoint(IAdminThemeService service)
    : Endpoint<UpdateThemeRequest, ApiResult<AdminThemeDetail>>
{
    public override void Configure()
    {
        Put("/admin/themes/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateThemeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var detail = await service.UpdateAsync(actorId, req.Id,
            new AdminUpdateThemeRequest
            {
                Code = req.Code,
                Name = req.Name,
                NameArabic = req.NameArabic,
                Description = req.Description,
                DescriptionArabic = req.DescriptionArabic,
                DisplayOrder = req.DisplayOrder,
                PageColor = req.PageColor,
                IsActive = req.IsActive,
            }, ct);
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateThemeRequest req, CancellationToken ct)
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
