// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs (alongside the rest of the TOTP suite)
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/pairing/verify</c> — confirms the
/// caller has successfully scanned the QR returned by
/// <c>GET /auth/totp/pairing</c>. Verifies the code against the active
/// authenticator secret WITHOUT mutating state — no replay-guard update,
/// no flag change, no audit row. The user can immediately use the same
/// code (or the next one) to sign in.
/// </summary>
public sealed class TotpPairingVerifyEndpoint(ITotpEnrollmentService totpEnrollment)
    : Endpoint<TotpConfirmRequest, ApiResult<TotpPairingVerifyResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/pairing/verify");
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Verifies a code against the current authenticator secret (no state mutation).");
    }

    public override async Task HandleAsync(TotpConfirmRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        var ok = await totpEnrollment.VerifyPairingAsync(userId, req.Code ?? string.Empty, ct);
        await Send.OkAsync(ApiResult<TotpPairingVerifyResponse>.Ok(
            new TotpPairingVerifyResponse(ok)), ct);
    }
}
