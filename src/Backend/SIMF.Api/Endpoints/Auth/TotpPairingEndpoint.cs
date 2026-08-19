// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs (alongside the rest of the TOTP suite)
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/pairing</c> — returns the QR + otpauth URI for
/// the caller's CURRENT authenticator secret, without rotating it, in exchange
/// for a code from that authenticator. Used by the CP's
/// <c>/account/totp-pairing</c> page to add a SECOND device. Returns 404 when the
/// account has no active secret yet — that caller belongs on
/// <c>POST /auth/totp/setup</c> instead.
///
/// <para>It is a POST, and it demands a code, because the response carries the
/// secret in plaintext. As a bodiless GET any holder of a stolen access token
/// could read it and mint codes for ever, which is the second factor ceasing to
/// be a second factor at the exact moment it matters. The endpoint used to
/// justify itself as re-pairing a LOST authenticator; that cannot happen, since
/// losing the authenticator means failing the second factor and never obtaining
/// the bearer token this endpoint requires.</para>
/// </summary>
public sealed class TotpPairingEndpoint(ITotpEnrollmentService totpEnrollment)
    : Endpoint<TotpConfirmRequest, ApiResult<TotpSetupResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/pairing");
        // No AllowAnonymous — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Returns the QR + otpauth URI for the current authenticator secret, "
            + "in exchange for a current code from it (no rotation).");
    }

    public override async Task HandleAsync(TotpConfirmRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await totpEnrollment.GetCurrentPairingAsync(
            userId, req.Code ?? string.Empty, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<TotpSetupResponse>.Ok(response), ct);
    }
}
