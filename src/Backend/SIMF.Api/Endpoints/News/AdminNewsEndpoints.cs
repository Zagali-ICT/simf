// Tests: SIMF.Api.Tests/NewsTests.cs
using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Api.Endpoints.News;

/// <summary>D-199 — admin CRUD over News (PR / marketing). Gated by the
/// per-action <c>News.*</c> permissions (Issue-1) + <see
/// cref="AuthorizationPolicies.RequireApprovedAccount"/>. The PublicRelations
/// role holds <c>News.*</c> as a seeded baseline grant, and Administrator holds
/// it via the permission wildcard, so both hit the same surface.</summary>
public sealed class ListNewsEndpoint(IAdminNewsService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminNewsSummary>>>
{
    public override void Configure()
    {
        Post("/admin/news/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.News.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminNewsSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetNewsRoute { public Guid Id { get; set; } }

public sealed class GetNewsEndpoint(IAdminNewsService service)
    : Endpoint<GetNewsRoute, ApiResult<AdminNewsDetail>>
{
    public override void Configure()
    {
        Get("/admin/news/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.News.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetNewsRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.NotFound, 404,
                "The news article was not found.",
                "لم يتم العثور على الخبر.");
        await Send.OkAsync(ApiResult<AdminNewsDetail>.Ok(detail), ct);
    }
}

public sealed class CreateNewsEndpoint(IAdminNewsService service)
    : Endpoint<CreateNewsRequest, ApiResult<AdminNewsDetail>>
{
    public override void Configure()
    {
        Post("/admin/news");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.News.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Validator<CreateNewsValidator>();
    }
    public override async Task HandleAsync(CreateNewsRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminNewsDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateNewsRoute : UpdateNewsRequest { public Guid Id { get; set; } }

public sealed class UpdateNewsEndpoint(IAdminNewsService service)
    : Endpoint<UpdateNewsRoute, ApiResult<AdminNewsDetail>>
{
    public override void Configure()
    {
        Put("/admin/news/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.News.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Validator<UpdateNewsValidator>();
    }
    public override async Task HandleAsync(UpdateNewsRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminNewsDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteNewsRoute { public Guid Id { get; set; } }

public sealed class DeleteNewsEndpoint(IAdminNewsService service)
    : Endpoint<DeleteNewsRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/news/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.News.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteNewsRoute req, CancellationToken ct)
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

/// <summary>FluentValidation lengths mirror <c>NewsConfiguration.HasMaxLength</c>
/// exactly (SIMF rule: validation length == EF length). The service re-checks
/// the same bounds so the contract holds even for non-HTTP callers.</summary>
public sealed class CreateNewsValidator : Validator<CreateNewsRequest>
{
    public CreateNewsValidator()
    {
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExcerptEn).MaximumLength(500);
        RuleFor(x => x.ExcerptAr).MaximumLength(500);
        RuleFor(x => x.BodyEn).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.BodyAr).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.CategoryEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CategoryAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ImageRelativePath).MaximumLength(512);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateNewsValidator : Validator<UpdateNewsRoute>
{
    public UpdateNewsValidator()
    {
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExcerptEn).MaximumLength(500);
        RuleFor(x => x.ExcerptAr).MaximumLength(500);
        RuleFor(x => x.BodyEn).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.BodyAr).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.CategoryEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CategoryAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ImageRelativePath).MaximumLength(512);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
