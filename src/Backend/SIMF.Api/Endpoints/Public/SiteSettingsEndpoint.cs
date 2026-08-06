// Tests: SIMF.Api.Tests/SiteSettingsPublicTests.cs
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Public, anonymous read of the CP-editable site/app branding
/// settings (the registration welcome message + the social links). Mirrors the
/// public-session read shape (anonymous, Tags "Public").</summary>
public sealed class GetSiteSettingsEndpoint(ISiteSettingsService service)
    : EndpointWithoutRequest<ApiResult<SiteSettingsResponse>>
{
    public override void Configure()
    {
        Get("/app/site-settings");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<SiteSettingsResponse>.Ok(
            await service.GetAsync(ct)), ct);
}
