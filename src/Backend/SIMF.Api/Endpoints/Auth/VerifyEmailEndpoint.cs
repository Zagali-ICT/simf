using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary><c>POST /api/v1/app/auth/verify-email</c> (SIMF-API-001 section 12.4).</summary>
public sealed class VerifyEmailEndpoint(IRegistrationService registrationService)
    : Endpoint<VerifyEmailRequest, ApiResult<VerifyEmailResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/verify-email");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Verify an email address with the code sent at sign-up.");
    }

    public override async Task HandleAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        var response = await registrationService.VerifyEmailAsync(req, ct);
        await Send.OkAsync(ApiResult<VerifyEmailResponse>.Ok(response), ct);
    }
}
