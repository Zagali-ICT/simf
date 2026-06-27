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

public sealed class UpdateCountryRequest
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>D-473 (#10) — invited to send a delegation (وفد). (Threaded through
    /// from D-499: this binding model previously omitted IsInvited, so editing a
    /// country silently reset the flag to false — fixed here.)</summary>
    public bool IsInvited { get; set; }

    /// <summary>D-499 (الوفود) — the invited delegation's arrival date (optional).</summary>
    public DateOnly? DelegationArrivalDate { get; set; }

    /// <summary>D-499 (الوفود) — the invited delegation's departure date (optional).</summary>
    public DateOnly? DelegationDepartureDate { get; set; }

    /// <summary>D-499 (الوفود) — the UserProfile id of the head of delegation
    /// (رئيس الوفد); must be an active delegate of this country (optional).</summary>
    public Guid? HeadOfDelegationUserProfileId { get; set; }
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
                new AdminUpdateCountryRequest
                {
                    Code = req.Code,
                    Name = req.Name,
                    NameArabic = req.NameArabic,
                    PhonePrefix = req.PhonePrefix,
                    DisplayOrder = req.DisplayOrder,
                    IsActive = req.IsActive,
                    IsInvited = req.IsInvited,
                    DelegationArrivalDate = req.DelegationArrivalDate,
                    DelegationDepartureDate = req.DelegationDepartureDate,
                    HeadOfDelegationUserProfileId = req.HeadOfDelegationUserProfileId,
                }, ct)), ct);
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
