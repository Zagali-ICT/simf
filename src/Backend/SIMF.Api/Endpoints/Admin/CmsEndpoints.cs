// Tests: SIMF.Api.Tests/CmsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Cms.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

// -- D-173 (gap doc G8) — admin CMS endpoints (ContentBlock + Banner) ----

public sealed class ListContentBlocksEndpoint(IAdminCmsService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminContentBlockSummary>>>
{
    public override void Configure()
    {
        Post("/admin/content-blocks/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminContentBlockSummary>>.Ok(
            await service.ListContentBlocksAsync(req, ct)), ct);
}

public sealed class GetContentBlockRoute { public string Key { get; set; } = string.Empty; }

public sealed class GetContentBlockEndpoint(IAdminCmsService service)
    : Endpoint<GetContentBlockRoute, ApiResult<AdminContentBlockSummary>>
{
    public override void Configure()
    {
        Get("/admin/content-blocks/{key}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetContentBlockRoute req, CancellationToken ct)
    {
        var summary = await service.GetContentBlockAsync(req.Key, ct)
            ?? throw new ApiException(
                ErrorCodes.ContentBlockNotFound, 404,
                "Content block not found.",
                "لم يتم العثور على المحتوى.");
        await Send.OkAsync(ApiResult<AdminContentBlockSummary>.Ok(summary), ct);
    }
}

public sealed class UpsertContentBlockEndpoint(IAdminCmsService service)
    : Endpoint<UpsertContentBlockRequest, ApiResult<AdminContentBlockSummary>>
{
    public override void Configure()
    {
        Put("/admin/content-blocks");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpsertContentBlockRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminContentBlockSummary>.Ok(
            await service.UpsertContentBlockAsync(actorId, req, ct)), ct);
    }
}

public sealed class DeleteContentBlockRoute { public string Key { get; set; } = string.Empty; }

public sealed class DeleteContentBlockEndpoint(IAdminCmsService service)
    : Endpoint<DeleteContentBlockRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/content-blocks/{key}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteContentBlockRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateContentBlockAsync(actorId, req.Key, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class ListBannersEndpoint(IAdminCmsService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminBannerSummary>>>
{
    public override void Configure()
    {
        Post("/admin/banners/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminBannerSummary>>.Ok(
            await service.ListBannersAsync(req, ct)), ct);
}

public sealed class GetBannerRoute { public Guid Id { get; set; } }

public sealed class GetBannerEndpoint(IAdminCmsService service)
    : Endpoint<GetBannerRoute, ApiResult<AdminBannerDetail>>
{
    public override void Configure()
    {
        Get("/admin/banners/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetBannerRoute req, CancellationToken ct)
    {
        var detail = await service.GetBannerAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.BannerNotFound, 404,
                "Banner not found.",
                "لم يتم العثور على البانر.");
        await Send.OkAsync(ApiResult<AdminBannerDetail>.Ok(detail), ct);
    }
}

public sealed class CreateBannerEndpoint(IAdminCmsService service)
    : Endpoint<CreateBannerRequest, ApiResult<AdminBannerDetail>>
{
    public override void Configure()
    {
        Post("/admin/banners");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CreateBannerRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminBannerDetail>.Ok(
            await service.CreateBannerAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateBannerRoute : UpdateBannerRequest { public Guid Id { get; set; } }

public sealed class UpdateBannerEndpoint(IAdminCmsService service)
    : Endpoint<UpdateBannerRoute, ApiResult<AdminBannerDetail>>
{
    public override void Configure()
    {
        Put("/admin/banners/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateBannerRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminBannerDetail>.Ok(
            await service.UpdateBannerAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteBannerRoute { public Guid Id { get; set; } }

public sealed class DeleteBannerEndpoint(IAdminCmsService service)
    : Endpoint<DeleteBannerRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/banners/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteBannerRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateBannerAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
