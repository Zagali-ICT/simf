// Tests: SIMF.Api.Tests/AdminProfileTypeTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
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
        var actorId = User.ActorId();
        var summary = await service.CreateAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminProfileTypeSummary>.Ok(summary), ct);
    }
}

/// <summary>D-115 — <c>PUT /api/v1/admin/profile-types/{id}</c>. UserType is NOT
/// updatable post-creation. D-186: IsVisitor IS updatable (audience-vs-partner
/// queue routing).
///
/// <para>D-843 — inherits the contract per D-505 (see <c>UpdateHallRoute</c>). It
/// used to re-declare the fields and the endpoint hand-copied them; it omitted
/// <c>ShowInPartnerDirectory</c>, whose contract default is <c>true</c>, so the
/// drop failed OPEN: unticking "show in partner directory" returned a success
/// toast and silently re-exposed the type in the networking surfaces. The same
/// class had already cost this DTO <c>IsVisitor</c> once before. UserType stays
/// non-updatable because the CONTRACT omits it, which is where that rule
/// belongs — not in a hand-maintained copy of the field list.</para></summary>
public sealed class UpdateAdminProfileTypeRouteRequest : AdminUpdateProfileTypeRequest
{
    public Guid Id { get; set; }
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
        var actorId = User.ActorId();
        // D-843 — pass the bound request straight through. No hand-copy, so no
        // field can be dropped from it.
        var summary = await service.UpdateAsync(actorId, req.Id, req, ct);
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
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
