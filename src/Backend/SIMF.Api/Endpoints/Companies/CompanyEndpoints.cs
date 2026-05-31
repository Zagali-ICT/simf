// Tests: SIMF.Api.Tests/CompaniesTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Companies.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Companies;

namespace SIMF.Api.Endpoints.Companies;

/// <summary>D-199 #3 — admin company list. Mirrors ListSponsorsEndpoint.</summary>
public sealed class ListCompaniesEndpoint(IAdminCompanyService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminCompanySummary>>>
{
    public override void Configure()
    {
        Post("/admin/companies/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminCompanySummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetCompanyRoute { public Guid Id { get; set; } }

public sealed class GetCompanyEndpoint(IAdminCompanyService service)
    : Endpoint<GetCompanyRoute, ApiResult<AdminCompanyDetail>>
{
    public override void Configure()
    {
        Get("/admin/companies/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetCompanyRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.CompanyNotFound, 404,
                "Company not found.",
                "لم يتم العثور على الشركة.");
        await Send.OkAsync(ApiResult<AdminCompanyDetail>.Ok(detail), ct);
    }
}

public sealed class CreateCompanyEndpoint(IAdminCompanyService service)
    : Endpoint<CreateCompanyRequest, ApiResult<AdminCompanyDetail>>
{
    public override void Configure()
    {
        Post("/admin/companies");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminCompanyDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateCompanyRoute : UpdateCompanyRequest { public Guid Id { get; set; } }

public sealed class UpdateCompanyEndpoint(IAdminCompanyService service)
    : Endpoint<UpdateCompanyRoute, ApiResult<AdminCompanyDetail>>
{
    public override void Configure()
    {
        Put("/admin/companies/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateCompanyRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminCompanyDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteCompanyRoute { public Guid Id { get; set; } }

public sealed class DeleteCompanyEndpoint(IAdminCompanyService service)
    : Endpoint<DeleteCompanyRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/companies/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeleteCompanyRoute req, CancellationToken ct)
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

// -- Company account provisioning --

public sealed class ListCompanyAccountsRoute { public Guid Id { get; set; } }

public sealed class ListCompanyAccountsEndpoint(IAdminCompanyService service)
    : Endpoint<ListCompanyAccountsRoute, ApiResult<IReadOnlyList<CompanyAccountSummary>>>
{
    public override void Configure()
    {
        Get("/admin/companies/{id:guid}/accounts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ListCompanyAccountsRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<CompanyAccountSummary>>.Ok(
            await service.ListAccountsAsync(req.Id, ct)), ct);
}

public sealed class ProvisionCompanyAccountRoute : ProvisionCompanyAccountRequest
{
    public Guid Id { get; set; }
}

public sealed class ProvisionCompanyAccountEndpoint(IAdminCompanyService service)
    : Endpoint<ProvisionCompanyAccountRoute, ApiResult<CompanyAccountSummary>>
{
    public override void Configure()
    {
        Post("/admin/companies/{id:guid}/accounts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Companies.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ProvisionCompanyAccountRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<CompanyAccountSummary>.Ok(
            await service.ProvisionAccountAsync(actorId, req.Id, req, ct)), ct);
    }
}
