// Tests: SIMF.Api.Tests/AppVersionPolicyPublicTests.cs
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Public, anonymous read of the mobile app-update policy
/// (per-platform min/latest version + store URL, admin-edited on the CP
/// configuration page). The app calls this on every launch and from the
/// About-the-app manual check; it fails open on any error, so this endpoint
/// carries no auth and no dedicated rate-limit bucket (anonymous
/// launch traffic must never drain the auth bucket; the global per-IP limiter
/// applies). Mirrors the site-settings read shape.</summary>
public sealed class GetAppVersionPolicyEndpoint(IAppVersionPolicyService service)
    : EndpointWithoutRequest<ApiResult<AppVersionPolicyResponse>>
{
    public override void Configure()
    {
        Get("/app/version-policy");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AppVersionPolicyResponse>.Ok(
            await service.GetAsync(ct)), ct);
}
