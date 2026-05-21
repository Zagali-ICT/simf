using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/change-password</c> — an authenticated user changes
/// their own password (SIMF-API-001 section 12.4). Requires a valid access token.
/// </summary>
public sealed class ChangePasswordEndpoint(IPasswordService passwordService)
    : Endpoint<ChangePasswordRequest, ApiResult<PasswordChangedResponse>>
{
    public override void Configure()
    {
        Post("/auth/change-password");
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Change your own password (requires sign-in).");
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await passwordService.ChangePasswordAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<PasswordChangedResponse>.Ok(response), ct);
    }
}
