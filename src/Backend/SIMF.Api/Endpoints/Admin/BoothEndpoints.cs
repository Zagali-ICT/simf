// Tests: SIMF.Api.Tests/AdminBoothsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Exhibition;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-199 — admin CRUD over <c>Booths</c> (Exhibition module).
/// Mirrors SpeakerEndpoints / HallEndpoints shape.</summary>
public sealed class ListBoothsEndpoint(IAdminBoothService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminBoothSummary>>>
{
    public override void Configure()
    {
        Post("/admin/booths/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Booths.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminBoothSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetBoothRequest { public Guid Id { get; set; } }

public sealed class GetBoothEndpoint(IAdminBoothService service)
    : Endpoint<GetBoothRequest, ApiResult<AdminBoothDetail>>
{
    public override void Configure()
    {
        Get("/admin/booths/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Booths.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetBoothRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");
        await Send.OkAsync(ApiResult<AdminBoothDetail>.Ok(detail), ct);
    }
}

public sealed class CreateBoothEndpoint(IAdminBoothService service)
    : Endpoint<AdminCreateBoothRequest, ApiResult<AdminBoothDetail>>
{
    public override void Configure()
    {
        Post("/admin/booths");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Booths.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateBoothRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminBoothDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateBoothRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateBoothEndpoint(IAdminBoothService service)
    : Endpoint<UpdateBoothRequest, ApiResult<AdminBoothDetail>>
{
    public override void Configure()
    {
        Put("/admin/booths/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Booths.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateBoothRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminBoothDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateBoothRequest
                {
                    Code = req.Code,
                    Name = req.Name,
                    NameArabic = req.NameArabic,
                    ExhibitorId = req.ExhibitorId,
                    OfficerName = req.OfficerName,
                    OfficerPhone = req.OfficerPhone,
                    OfficerEmail = req.OfficerEmail,
                    Sector = req.Sector,
                    SectorArabic = req.SectorArabic,
                    Description = req.Description,
                    DescriptionArabic = req.DescriptionArabic,
                    HallId = req.HallId,
                    MapX = req.MapX,
                    MapY = req.MapY,
                    IsActive = req.IsActive,
                }, ct)), ct);
    }
}

public sealed class DeactivateBoothRequest { public Guid Id { get; set; } }

public sealed class DeactivateBoothEndpoint(IAdminBoothService service)
    : Endpoint<DeactivateBoothRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/booths/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Booths.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateBoothRequest req, CancellationToken ct)
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
