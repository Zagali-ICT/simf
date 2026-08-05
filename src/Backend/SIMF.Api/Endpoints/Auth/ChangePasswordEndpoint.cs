using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/change-password</c> — an authenticated user changes
/// their own password (SIMF-API-001 section 12.4). Requires a valid access token.
/// </summary>
public sealed class ChangePasswordEndpoint(IPasswordService passwordService)
    : Endpoint<ChangePasswordRequest, ApiResult<ChangePasswordResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/change-password");
        // No AllowAnonymous() — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Change your own password (requires sign-in).");
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await passwordService.ChangePasswordAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<ChangePasswordResponse>.Ok(response), ct);
    }
}
