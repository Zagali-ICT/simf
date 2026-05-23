// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>DELETE /api/v1/account/avatar</c> — removes the signed-in user's avatar
/// (myComment #11). Idempotent — succeeds whether or not one is currently set.
/// </summary>
public sealed class AvatarDeleteEndpoint(IAccountService accountService)
    : EndpointWithoutRequest<ApiResult<AvatarResponse>>
{
    public override void Configure()
    {
        Delete("/account/avatar");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Remove the signed-in user's avatar.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await accountService.RemoveAvatarAsync(userId, ct);
        await Send.OkAsync(ApiResult<AvatarResponse>.Ok(response), ct);
    }
}
