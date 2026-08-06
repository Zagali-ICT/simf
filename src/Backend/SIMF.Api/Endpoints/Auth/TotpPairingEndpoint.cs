// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs (alongside the rest of the TOTP suite)
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>GET /api/v1/app/auth/totp/pairing</c> — returns the QR + otpauth
/// URI for the caller's CURRENT authenticator secret without rotating it.
/// Used by the CP's <c>/account/totp-pairing</c> page so an admin whose
/// authenticator device was lost can re-scan against the same secret the
/// sign-in flow will continue to verify against. Returns 404 when the
/// account has no active authenticator secret yet — the caller should
/// route the user through <c>POST /auth/totp/setup</c> instead.
/// </summary>
public sealed class TotpPairingEndpoint(ITotpEnrollmentService totpEnrollment)
    : EndpointWithoutRequest<ApiResult<TotpSetupResponse>>
{
    public override void Configure()
    {
        Get("/app/auth/totp/pairing");
        // No AllowAnonymous — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Returns the QR + otpauth URI for the current authenticator secret (no rotation).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await totpEnrollment.GetCurrentPairingAsync(userId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<TotpSetupResponse>.Ok(response), ct);
    }
}
