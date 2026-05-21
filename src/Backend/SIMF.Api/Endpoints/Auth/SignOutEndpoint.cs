using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/sign-out</c> — ends every session for the authenticated
/// caller (SIMF-API-001 section 12.4). Requires a valid access token.
/// </summary>
public sealed class SignOutEndpoint(ISessionService sessionService)
    : EndpointWithoutRequest<ApiResult<SignOutResponse>>
{
    public override void Configure()
    {
        Post("/auth/sign-out");
        // No AllowAnonymous() — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Sign out and end every session for the account.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await sessionService.SignOutAsync(userId, ct);
        await Send.OkAsync(ApiResult<SignOutResponse>.Ok(new SignOutResponse(true)), ct);
    }
}
