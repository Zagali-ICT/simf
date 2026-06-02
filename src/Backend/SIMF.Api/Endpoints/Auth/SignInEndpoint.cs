using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary><c>POST /api/v1/auth/sign-in</c> — the password step (SIMF-API-001 section 12.4).</summary>
public sealed class SignInEndpoint(ISignInService signInService)
    : Endpoint<SignInRequest, ApiResult<SignInResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/sign-in");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Summary(summary => summary.Summary =
            "Sign in with email and password; returns the second-factor challenge.");
    }

    public override async Task HandleAsync(SignInRequest req, CancellationToken ct)
    {
        var response = await signInService.SignInAsync(req, ct);
        await Send.OkAsync(ApiResult<SignInResponse>.Ok(response), ct);
    }
}
