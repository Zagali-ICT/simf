// Tests: SIMF.Api.Tests/OrganizationProfileTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Organization;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-495 — the CP Organization Profile editor: read the full profile
/// (incl. child-row ids) for the form. Gated by OrganizationProfile.View.</summary>
public sealed class GetAdminOrganizationProfileEndpoint(
    IOrganizationProfileAdminService service)
    : EndpointWithoutRequest<ApiResult<OrganizationProfileResponse>>
{
    public override void Configure()
    {
        Get("/admin/organization-profile");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OrganizationProfile.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<OrganizationProfileResponse>.Ok(
            await service.GetAsync(ct)), ct);
}

/// <summary>D-495 — save the Organization Profile (full-document upsert), then
/// return the re-read effective profile. Gated by OrganizationProfile.Manage.</summary>
public sealed class SaveAdminOrganizationProfileEndpoint(
    IOrganizationProfileAdminService service)
    : Endpoint<AdminUpdateOrganizationProfileRequest, ApiResult<OrganizationProfileResponse>>
{
    public override void Configure()
    {
        Put("/admin/organization-profile");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OrganizationProfile.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        AdminUpdateOrganizationProfileRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.UpdateAsync(req, actorId, ct);
        await Send.OkAsync(ApiResult<OrganizationProfileResponse>.Ok(
            await service.GetAsync(ct)), ct);
    }
}
