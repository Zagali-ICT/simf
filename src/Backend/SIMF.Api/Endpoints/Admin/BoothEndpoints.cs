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

/// <summary>D-844 — binds {id} + body via a derived route that INHERITS the
/// contract, per D-505 (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// D-842 (sessions), D-843 (gates, profile types) and the four before them
/// silently dropped a field on PUT. Passing the bound request straight through
/// makes that drop impossible.</summary>
public sealed class UpdateBoothRequest : AdminUpdateBoothRequest
{
    public Guid Id { get; set; }
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
                req, ct)), ct);
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
