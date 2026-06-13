using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/reset-password</c> — sets a new password with the
/// emailed reset code (SIMF-API-001 section 12.4).
/// </summary>
public sealed class ResetPasswordEndpoint(IPasswordService passwordService)
    : Endpoint<ResetPasswordRequest, ApiResult<ResetPasswordResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/reset-password");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Summary(summary => summary.Summary = "Set a new password using the emailed reset code.");
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var response = await passwordService.ResetPasswordAsync(req, ct);
        await Send.OkAsync(ApiResult<ResetPasswordResponse>.Ok(response), ct);
    }
}
