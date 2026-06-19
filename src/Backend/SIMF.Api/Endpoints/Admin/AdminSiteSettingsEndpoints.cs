// Tests: SIMF.Api.Tests/SiteSettingsAdminTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Configuration;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-464 — the CP "Site Settings" page: read the current branding values
/// (registration welcome message + social links). Reuses the public read-path so
/// the form prefills with the effective values. Gated by Configuration.View.</summary>
public sealed class GetAdminSiteSettingsEndpoint(ISiteSettingsService siteSettings)
    : EndpointWithoutRequest<ApiResult<SiteSettingsResponse>>
{
    public override void Configure()
    {
        Get("/admin/site-settings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<SiteSettingsResponse>.Ok(
            await siteSettings.GetAsync(ct)), ct);
}

/// <summary>D-464 — save the CP "Site Settings" page (upserts the whitelisted
/// keys), then returns the re-read effective values. Gated by Configuration.Edit.</summary>
public sealed class SaveAdminSiteSettingsEndpoint(
    IAdminSystemSettingService admin,
    ISiteSettingsService siteSettings)
    : Endpoint<AdminUpdateSiteSettingsRequest, ApiResult<SiteSettingsResponse>>
{
    public override void Configure()
    {
        Put("/admin/site-settings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminUpdateSiteSettingsRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await admin.SaveSiteSettingsAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<SiteSettingsResponse>.Ok(
            await siteSettings.GetAsync(ct)), ct);
    }
}
