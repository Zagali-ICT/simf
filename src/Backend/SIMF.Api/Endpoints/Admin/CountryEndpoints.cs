// Tests: SIMF.Api.Tests/AdminCountriesTests.cs
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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

public sealed class UpdateCountryRequest
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateCountryEndpoint(IAdminCountryService service)
    : Endpoint<UpdateCountryRequest, ApiResult<AdminCountryDetail>>
{
    public override void Configure()
    {
        Put("/admin/countries/{id:int}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
                new AdminUpdateCountryRequest
                {
                    Code = req.Code,
                    NameEn = req.NameEn,
                    NameAr = req.NameAr,
                    PhonePrefix = req.PhonePrefix,
                    DisplayOrder = req.DisplayOrder,
                    IsActive = req.IsActive,
                }, ct)), ct);
    }
}

public sealed class DeactivateCountryRequest { public int Id { get; set; } }

public sealed class DeactivateCountryEndpoint(IAdminCountryService service)
    : Endpoint<DeactivateCountryRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/countries/{id:int}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
