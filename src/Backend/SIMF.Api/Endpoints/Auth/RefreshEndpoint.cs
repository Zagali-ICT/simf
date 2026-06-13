using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/refresh</c> — exchanges a refresh token for a new
/// access token and a rotated refresh token (SIMF-API-001 section 12.4).
/// </summary>
public sealed class RefreshEndpoint(ISessionService sessionService)
    : Endpoint<RefreshRequest, ApiResult<AuthTokens>>
{
    public override void Configure()
    {
        Post("/app/auth/refresh");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Exchange a refresh token for a new access token; the refresh token is rotated.");
    }

    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        var tokens = await sessionService.RefreshAsync(req, ct);
        await Send.OkAsync(ApiResult<AuthTokens>.Ok(tokens), ct);
    }
}
