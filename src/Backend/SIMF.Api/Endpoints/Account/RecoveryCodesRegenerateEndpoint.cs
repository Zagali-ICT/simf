// Tests: SIMF.Api.Tests/RecoveryCodesTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>POST /api/v1/app/account/recovery-codes/regenerate</c> — mints a fresh batch
/// of 10 single-use recovery codes for the signed-in user, invalidating any
/// previous batch (D-040). Plaintext codes are returned exactly once.
/// </summary>
public sealed class RecoveryCodesRegenerateEndpoint(IAccountService accountService)
    : EndpointWithoutRequest<ApiResult<RecoveryCodesResponse>>
{
    public override void Configure()
    {
        Post("/app/account/recovery-codes/regenerate");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Regenerate the signed-in user's TOTP recovery codes.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await accountService.RegenerateRecoveryCodesAsync(userId, ct);
        await Send.OkAsync(ApiResult<RecoveryCodesResponse>.Ok(response), ct);
    }
}
