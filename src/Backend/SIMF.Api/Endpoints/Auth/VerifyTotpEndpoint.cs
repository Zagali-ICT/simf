using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/verify-totp</c> — completes a Control Panel sign-in with
/// the authenticator TOTP code (SIMF-API-001 section 12.4).
/// </summary>
public sealed class VerifyTotpEndpoint(ISignInService signInService)
    : Endpoint<VerifyTotpRequest, ApiResult<AuthTokens>>
{
    public override void Configure()
    {
        Post("/app/auth/verify-totp");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Complete a Control Panel sign-in with the TOTP code.");
    }

    public override async Task HandleAsync(VerifyTotpRequest req, CancellationToken ct)
    {
        var tokens = await signInService.VerifyTotpAsync(req, ct);
        await Send.OkAsync(ApiResult<AuthTokens>.Ok(tokens), ct);
    }
}
