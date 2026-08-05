using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/sign-out</c> — ends every session for the authenticated
/// caller (SIMF-API-001 section 12.4). Requires a valid access token.
/// </summary>
public sealed class SignOutEndpoint(ISessionService sessionService)
    : EndpointWithoutRequest<ApiResult<SignOutResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/sign-out");
        // No AllowAnonymous() — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Sign out and end every session for the account.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();

        await sessionService.SignOutAsync(userId, ct);
        await Send.OkAsync(ApiResult<SignOutResponse>.Ok(new SignOutResponse(true)), ct);
    }
}
