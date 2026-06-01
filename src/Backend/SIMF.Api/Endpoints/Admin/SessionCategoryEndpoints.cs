// Tests: SIMF.Api.Tests/SessionCategoriesTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

// B9b — D-226: admin CRUD over the dynamic SessionCategory lookup. Validation
// lives in the service (mirrors AdminOrganisationService); endpoints gate +
// bind + delegate. The PUT reads the route GUID via Route<Guid>("id") (the
// Organisation/FAQ pattern); the request carries no id.
public sealed class SessionCategoryByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class ListSessionCategoriesEndpoint(IAdminSessionCategoryService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSessionCategorySummary>>>
{
    public override void Configure()
    {
        Post("/admin/session-categories/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionCategories.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSessionCategorySummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetSessionCategoryEndpoint(IAdminSessionCategoryService service)
    : Endpoint<SessionCategoryByIdRequest, ApiResult<AdminSessionCategoryDetail>>
{
    public override void Configure()
    {
        Get("/admin/session-categories/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionCategories.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(SessionCategoryByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionCategoryNotFound, 404,
                "The session category was not found.",
                "لم يتم العثور على تصنيف الجلسة.");
        await Send.OkAsync(ApiResult<AdminSessionCategoryDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSessionCategoryEndpoint(IAdminSessionCategoryService service)
    : Endpoint<AdminCreateSessionCategoryRequest, ApiResult<AdminSessionCategoryDetail>>
{
    public override void Configure()
    {
        Post("/admin/session-categories");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionCategories.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateSessionCategoryRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSessionCategoryDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateSessionCategoryEndpoint(IAdminSessionCategoryService service)
    : Endpoint<AdminUpdateSessionCategoryRequest, ApiResult<AdminSessionCategoryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-categories/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionCategories.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminUpdateSessionCategoryRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSessionCategoryDetail>.Ok(
            await service.UpdateAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeactivateSessionCategoryEndpoint(IAdminSessionCategoryService service)
    : Endpoint<SessionCategoryByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/session-categories/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionCategories.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(SessionCategoryByIdRequest req, CancellationToken ct)
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
