using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/forgot-password</c> — emails a password-reset code
/// (SIMF-API-001 section 12.4). The response never reveals whether the account
/// exists.
/// </summary>
public sealed class ForgotPasswordEndpoint(IPasswordService passwordService)
    : Endpoint<ForgotPasswordRequest, ApiResult<ForgotPasswordResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/forgot-password");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Summary(summary => summary.Summary = "Request a password-reset code by email.");
    }

    public override async Task HandleAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var response = await passwordService.ForgotPasswordAsync(req, ct);
        await Send.OkAsync(ApiResult<ForgotPasswordResponse>.Ok(response), ct);
    }
}
