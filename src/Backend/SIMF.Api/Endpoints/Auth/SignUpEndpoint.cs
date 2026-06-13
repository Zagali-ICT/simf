using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary><c>POST /api/v1/app/auth/sign-up</c> (SIMF-API-001 section 12.4).</summary>
public sealed class SignUpEndpoint(IRegistrationService registrationService)
    : Endpoint<SignUpRequest, ApiResult<SignUpResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/sign-up");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create an account and send a six-digit email verification code.");
    }

    public override async Task HandleAsync(SignUpRequest req, CancellationToken ct)
    {
        var response = await registrationService.SignUpAsync(req, ct);
        await Send.ResponseAsync(ApiResult<SignUpResponse>.Ok(response), 201, ct);
    }
}
