using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/verify-otp</c> — completes a visitor sign-in with the
/// emailed one-time code (SIMF-API-001 Amendment A.1).
/// </summary>
public sealed class VerifyOtpEndpoint(ISignInService signInService)
    : Endpoint<VerifyOtpRequest, ApiResult<AuthTokens>>
{
    public override void Configure()
    {
        Post("/app/auth/verify-otp");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Complete a visitor sign-in with the emailed one-time code.");
    }

    public override async Task HandleAsync(VerifyOtpRequest req, CancellationToken ct)
    {
        var tokens = await signInService.VerifyOtpAsync(req, ct);
        await Send.OkAsync(ApiResult<AuthTokens>.Ok(tokens), ct);
    }
}
