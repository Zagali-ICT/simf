using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/complete-password-change</c> — D-206: sets a new
/// password against the single-use ticket the sign-in step issued for a Control
/// Panel account holding a forced-change (seeded/admin-rotated) credential.
/// Anonymous like <c>verify-totp</c> — the ticket, not a session, authorises the
/// call. No tokens are minted; the operator signs in again afterwards.
/// </summary>
public sealed class CompletePasswordChangeEndpoint(IPasswordService passwordService)
    : Endpoint<CompletePasswordChangeRequest, ApiResult<CompletePasswordChangeResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/complete-password-change");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Set a new password against a forced-change sign-in ticket.");
    }

    public override async Task HandleAsync(CompletePasswordChangeRequest req, CancellationToken ct)
    {
        var response = await passwordService.CompletePasswordChangeAsync(req, ct);
        await Send.OkAsync(ApiResult<CompletePasswordChangeResponse>.Ok(response), ct);
    }
}
