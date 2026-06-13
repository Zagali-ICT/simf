using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary><c>POST /api/v1/app/auth/resend-code</c> (SIMF-API-001 section 12.4).</summary>
public sealed class ResendCodeEndpoint(IRegistrationService registrationService)
    : Endpoint<ResendCodeRequest, ApiResult<ResendCodeResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/resend-code");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Issue a fresh email verification code and invalidate the previous one.");
    }

    public override async Task HandleAsync(ResendCodeRequest req, CancellationToken ct)
    {
        var response = await registrationService.ResendCodeAsync(req, ct);
        await Send.OkAsync(ApiResult<ResendCodeResponse>.Ok(response), ct);
    }
}
