// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/profile</c> — the signed-in user's profile
/// (myComment item #11). Returns id, email, display name, the avatar as a
/// data URI, the two-factor flag and the roles.
/// </summary>
public sealed class ProfileEndpoint(IAccountService accountService)
    : EndpointWithoutRequest<ApiResult<ProfileResponse>>
{
    public override void Configure()
    {
        Get("/app/account/profile");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Get the signed-in user's profile (requires sign-in).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await accountService.GetProfileAsync(userId, ct);
        await Send.OkAsync(ApiResult<ProfileResponse>.Ok(response), ct);
    }
}
