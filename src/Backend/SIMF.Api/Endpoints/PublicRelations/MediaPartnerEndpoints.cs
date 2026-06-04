// Tests: SIMF.Api.Tests/MediaPartnersTests.cs, SIMF.Api.Tests/AdminMediaPartnersTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Api.Endpoints.PublicRelations;

/// <summary>D-199 (Mockup page 31 — "شركاء النجاح") — public anonymous list
/// of active media partners, ordered for the app's media grid. Mirrors the
/// ListPublicDelegationsEndpoint shape (anonymous public read).</summary>
public sealed class ListPublicMediaPartnersEndpoint(IPublicMediaPartnerService service)
    : EndpointWithoutRequest<ApiResult<PublicMediaPartners>>
{
    public override void Configure()
    {
        Get("/app/media-partners");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicMediaPartners>.Ok(
            await service.ListAsync(ct)), ct);
}

// -- Admin media-partner CRUD (mirrors CountryEndpoints / SpeakerEndpoints) --

public sealed class ListMediaPartnersEndpoint(IAdminMediaPartnerService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminMediaPartnerSummary>>>
{
    public override void Configure()
    {
        Post("/admin/media-partners/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MediaPartners.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminMediaPartnerSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetMediaPartnerRequest { public Guid Id { get; set; } }

public sealed class GetMediaPartnerEndpoint(IAdminMediaPartnerService service)
    : Endpoint<GetMediaPartnerRequest, ApiResult<AdminMediaPartnerDetail>>
{
    public override void Configure()
    {
        Get("/admin/media-partners/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MediaPartners.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetMediaPartnerRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");
        await Send.OkAsync(ApiResult<AdminMediaPartnerDetail>.Ok(detail), ct);
    }
}

public sealed class CreateMediaPartnerEndpoint(IAdminMediaPartnerService service)
    : Endpoint<AdminCreateMediaPartnerRequest, ApiResult<AdminMediaPartnerDetail>>
{
    public override void Configure()
    {
        Post("/admin/media-partners");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MediaPartners.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateMediaPartnerRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminMediaPartnerDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateMediaPartnerRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? ContactId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateMediaPartnerEndpoint(IAdminMediaPartnerService service)
    : Endpoint<UpdateMediaPartnerRequest, ApiResult<AdminMediaPartnerDetail>>
{
    public override void Configure()
    {
        Put("/admin/media-partners/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MediaPartners.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateMediaPartnerRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminMediaPartnerDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateMediaPartnerRequest
                {
                    Name = req.Name,
                    NameArabic = req.NameArabic,
                    LogoRelativePath = req.LogoRelativePath,
                    Url = req.Url,
                    DisplayOrder = req.DisplayOrder,
                    ContactId = req.ContactId,
                    IsActive = req.IsActive,
                }, ct)), ct);
    }
}

public sealed class DeactivateMediaPartnerRequest { public Guid Id { get; set; } }

public sealed class DeactivateMediaPartnerEndpoint(IAdminMediaPartnerService service)
    : Endpoint<DeactivateMediaPartnerRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/media-partners/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MediaPartners.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateMediaPartnerRequest req, CancellationToken ct)
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
