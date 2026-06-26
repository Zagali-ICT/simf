// Tests: SIMF.Api.Tests/SponsorsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sponsors;

namespace SIMF.Api.Endpoints.Sponsors;

/// <summary>D-199 (Mockup page 23) — public anonymous list of active sponsors,
/// grouped by tier. Mirrors ListPublicDelegationsEndpoint (AllowAnonymous,
/// Tags("Public")).</summary>
public sealed class ListPublicSponsorsEndpoint(IPublicSponsorService service)
    : EndpointWithoutRequest<ApiResult<PublicSponsors>>
{
    public override void Configure()
    {
        Get("/app/sponsors");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicSponsors>.Ok(
            await service.ListAsync(ct)), ct);
}

/// <summary>Wave 3 (Figma 1439:11826 "الراعي") — public anonymous sponsor detail
/// (about, city, website, tier, country). A 404 when the sponsor is missing /
/// inactive.</summary>
public sealed class GetPublicSponsorRequest { public Guid Id { get; set; } }

public sealed class GetPublicSponsorEndpoint(IPublicSponsorService service)
    : Endpoint<GetPublicSponsorRequest, ApiResult<PublicSponsorDetail>>
{
    public override void Configure()
    {
        Get("/app/sponsors/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(GetPublicSponsorRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");
        await Send.OkAsync(ApiResult<PublicSponsorDetail>.Ok(detail), ct);
    }
}

// -- Admin sponsor CRUD --

public sealed class ListSponsorsEndpoint(IAdminSponsorService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSponsorSummary>>>
{
    public override void Configure()
    {
        Post("/admin/sponsors/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sponsors.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSponsorSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetSponsorRequest { public Guid Id { get; set; } }

public sealed class GetSponsorEndpoint(IAdminSponsorService service)
    : Endpoint<GetSponsorRequest, ApiResult<AdminSponsorDetail>>
{
    public override void Configure()
    {
        Get("/admin/sponsors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sponsors.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetSponsorRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");
        await Send.OkAsync(ApiResult<AdminSponsorDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSponsorEndpoint(IAdminSponsorService service)
    : Endpoint<AdminCreateSponsorRequest, ApiResult<AdminSponsorDetail>>
{
    public override void Configure()
    {
        Post("/admin/sponsors");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sponsors.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateSponsorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSponsorDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateSponsorRequest
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? ContactId { get; set; }
    public string? About { get; set; }
    public string? AboutArabic { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateSponsorEndpoint(IAdminSponsorService service)
    : Endpoint<UpdateSponsorRequest, ApiResult<AdminSponsorDetail>>
{
    public override void Configure()
    {
        Put("/admin/sponsors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sponsors.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateSponsorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSponsorDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateSponsorRequest
                {
                    NameEn = req.NameEn,
                    NameAr = req.NameAr,
                    Tier = req.Tier,
                    LogoRelativePath = req.LogoRelativePath,
                    Url = req.Url,
                    DisplayOrder = req.DisplayOrder,
                    ContactId = req.ContactId,
                    About = req.About,
                    AboutArabic = req.AboutArabic,
                    IsActive = req.IsActive,
                }, ct)), ct);
    }
}

public sealed class DeactivateSponsorRequest { public Guid Id { get; set; } }

public sealed class DeactivateSponsorEndpoint(IAdminSponsorService service)
    : Endpoint<DeactivateSponsorRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/sponsors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sponsors.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateSponsorRequest req, CancellationToken ct)
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
