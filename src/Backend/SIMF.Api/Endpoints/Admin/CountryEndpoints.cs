// Tests: SIMF.Api.Tests/AdminCountriesTests.cs, SIMF.Api.Tests/DelegationsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Common.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

public sealed class ListCountriesEndpoint(IAdminCountryService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminCountrySummary>>>
{
    public override void Configure()
    {
        Post("/admin/countries/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminCountrySummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetCountryRequest { public int Id { get; set; } }

public sealed class GetCountryEndpoint(IAdminCountryService service)
    : Endpoint<GetCountryRequest, ApiResult<AdminCountryDetail>>
{
    public override void Configure()
    {
        Get("/admin/countries/{id:int}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetCountryRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.CountryNotFound, 404,
                "The country was not found.",
                "لم يتم العثور على البلد.");
        await Send.OkAsync(ApiResult<AdminCountryDetail>.Ok(detail), ct);
    }
}

public sealed class CreateCountryEndpoint(IAdminCountryService service)
    : Endpoint<AdminCreateCountryRequest, ApiResult<AdminCountryDetail>>
{
    public override void Configure()
    {
        Post("/admin/countries");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateCountryRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminCountryDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>D-844 — binds {id} + body via a derived route that INHERITS the
/// contract, per D-505 (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// D-842 (sessions), D-843 (gates, profile types) and the four before them
/// silently dropped a field on PUT. Passing the bound request straight through
/// makes that drop impossible.</summary>
public sealed class UpdateCountryRequest : AdminUpdateCountryRequest
{
    // Countries are the one admin resource with an int key, not a Guid — the
    // route is {id:int}.
    public int Id { get; set; }
}

public sealed class UpdateCountryEndpoint(IAdminCountryService service)
    : Endpoint<UpdateCountryRequest, ApiResult<AdminCountryDetail>>
{
    public override void Configure()
    {
        Put("/admin/countries/{id:int}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateCountryRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminCountryDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                req, ct)), ct);
    }
}

/// <summary>D-499 (الوفود) — <c>GET /admin/countries/{id}/delegates</c>: the active
/// delegates of a country, feeding the head-of-delegation picker on the CP country
/// Edit form. Gated by <see cref="PermissionCatalog.Countries.View"/>.</summary>
public sealed class ListCountryDelegatesEndpoint(IAdminCountryService service)
    : Endpoint<GetCountryRequest, ApiResult<IReadOnlyList<AdminCountryDelegateOption>>>
{
    public override void Configure()
    {
        Get("/admin/countries/{id:int}/delegates");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetCountryRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminCountryDelegateOption>>.Ok(
            await service.ListDelegatesAsync(req.Id, ct)), ct);
}

public sealed class DeactivateCountryRequest { public int Id { get; set; } }

public sealed class DeactivateCountryEndpoint(IAdminCountryService service)
    : Endpoint<DeactivateCountryRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/countries/{id:int}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Countries.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeactivateCountryRequest req, CancellationToken ct)
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
