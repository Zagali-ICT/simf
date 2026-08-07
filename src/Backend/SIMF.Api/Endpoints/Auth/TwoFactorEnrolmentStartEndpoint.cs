// Tests: SIMF.Api.Tests/ControlPanelTwoFactorEnrolmentTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/enrolment/start</c> — #2 (Q1, 2026-07-30).
/// Step one of MANDATORY authenticator enrolment for a Control Panel account
/// that has just proved its password but carries no second factor. Returns the
/// fresh secret, the <c>otpauth://</c> URI and an SVG QR code.
///
/// <para>Anonymous by necessity, not by oversight: this endpoint runs BEFORE a
/// token exists, and the single-use, 15-minute, attempt-capped enrolment ticket
/// the sign-in step issued is the credential. It is on the reviewed allow-list
/// in <c>BusinessFlow13PermissionMatrixTests</c>.</para>
/// </summary>
public sealed class TwoFactorEnrolmentStartEndpoint(ISignInService signInService)
    : Endpoint<StartTwoFactorEnrolmentRequest, ApiResult<TotpSetupResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/enrolment/start");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Begin mandatory authenticator enrolment against a sign-in enrolment ticket.");
    }

    public override async Task HandleAsync(
        StartTwoFactorEnrolmentRequest req, CancellationToken ct)
    {
        var response = await signInService.StartTwoFactorEnrolmentAsync(req, ct);
        await Send.OkAsync(ApiResult<TotpSetupResponse>.Ok(response), ct);
    }
}
