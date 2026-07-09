// Tests: SIMF.Api.Tests/AdminProfileTypeTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-115 — <c>POST /api/v1/admin/profile-types/list</c>. Paged + filtered
/// grid of every ProfileType row. Mirrors the InterestEndpoints shape so
/// the CP can use the same SimfDataGrid primitive.
/// </summary>
public sealed class ListAdminProfileTypesEndpoint(IAdminProfileTypeCommandService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminProfileTypeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/profile-types/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of profile types. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAllAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminProfileTypeSummary>>.Ok(page), ct);
    }
}

/// <summary>D-115 — <c>GET /api/v1/admin/profile-types/{id}</c>.</summary>
public sealed class GetAdminProfileTypeRequest
{
    public Guid Id { get; set; }
}

public sealed class GetAdminProfileTypeEndpoint(IAdminProfileTypeCommandService service)
    : Endpoint<GetAdminProfileTypeRequest, ApiResult<AdminProfileTypeSummary>>
{
    public override void Configure()
    {
        Get("/admin/profile-types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one profile type. Requires Administrator role.");
    }

    public override async Task HandleAsync(GetAdminProfileTypeRequest req, CancellationToken ct)
    {
        var summary = await service.GetAsync(req.Id, ct);
        if (summary is null)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeNotFound, 404,
                "The profile type was not found.",
                "لم يتم العثور على نوع الملف الشخصي.");
        }
        await Send.OkAsync(ApiResult<AdminProfileTypeSummary>.Ok(summary), ct);
    }
}

/// <summary>D-115 — <c>POST /api/v1/admin/profile-types</c>. Create a new row.
/// UserType is restricted to Visitor or Other; per-UserType name uniqueness
/// is enforced server-side.</summary>
public sealed class CreateAdminProfileTypeEndpoint(IAdminProfileTypeCommandService service)
    : Endpoint<AdminCreateProfileTypeRequest, ApiResult<AdminProfileTypeSummary>>
{
    public override void Configure()
    {
        Post("/admin/profile-types");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new profile type. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminCreateProfileTypeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminProfileTypeSummary>.Ok(summary), ct);
    }
}

/// <summary>D-115 — <c>PUT /api/v1/admin/profile-types/{id}</c>. UserType is
/// NOT updatable post-creation — the route body does not carry it.
/// D-186: IsVisitor IS updatable (audience-vs-partner queue routing).</summary>
public sealed class UpdateAdminProfileTypeRouteRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string PageColor { get; set; } = string.Empty;
    /// <summary>D-161 — mobile-app authority assigned to this type.</summary>
    public string? MobileAppRole { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>D-186 — audience (true) or partner / staff (false).</summary>
    public bool IsVisitor { get; set; } = true;
    /// <summary>D-725 — whether the type appears in the app sign-up picker
    /// (false = CP-only, e.g. Staff / Moderator).</summary>
    public bool IsAppRegisterable { get; set; } = true;
}

public sealed class UpdateAdminProfileTypeEndpoint(IAdminProfileTypeCommandService service)
    : Endpoint<UpdateAdminProfileTypeRouteRequest, ApiResult<AdminProfileTypeSummary>>
{
    public override void Configure()
    {
        Put("/admin/profile-types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Update a profile type. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        UpdateAdminProfileTypeRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var summary = await service.UpdateAsync(actorId, req.Id,
            new AdminUpdateProfileTypeRequest
            {
                Name = req.Name,
                NameArabic = req.NameArabic,
                PageColor = req.PageColor,
                MobileAppRole = req.MobileAppRole,
                IsActive = req.IsActive,
                IsVisitor = req.IsVisitor,
                IsAppRegisterable = req.IsAppRegisterable,
            }, ct);
        await Send.OkAsync(ApiResult<AdminProfileTypeSummary>.Ok(summary), ct);
    }
}

/// <summary>D-115 — <c>DELETE /api/v1/admin/profile-types/{id}</c>.
/// Soft-delete (Idempotent). 409 if any UserProfile still references the
/// row.</summary>
public sealed class DeactivateAdminProfileTypeRequest
{
    public Guid Id { get; set; }
}

public sealed class DeactivateAdminProfileTypeEndpoint(IAdminProfileTypeCommandService service)
    : Endpoint<DeactivateAdminProfileTypeRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/profile-types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Deactivate (soft-delete) a profile type. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        DeactivateAdminProfileTypeRequest req, CancellationToken ct)
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
