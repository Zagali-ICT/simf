// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>DELETE /api/v1/app/account/avatar</c> — removes the signed-in user's avatar
/// (myComment #11). Idempotent — succeeds whether or not one is currently set.
/// </summary>
public sealed class AvatarDeleteEndpoint(IAccountService accountService)
    : EndpointWithoutRequest<ApiResult<AvatarResponse>>
{
    public override void Configure()
    {
        Delete("/app/account/avatar");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Remove the signed-in user's avatar.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await accountService.RemoveAvatarAsync(userId, ct);
        await Send.OkAsync(ApiResult<AvatarResponse>.Ok(response), ct);
    }
}
