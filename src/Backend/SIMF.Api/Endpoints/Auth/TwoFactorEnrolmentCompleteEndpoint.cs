// Tests: SIMF.Api.Tests/ControlPanelTwoFactorEnrolmentTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/enrolment/complete</c> — #2 (Q1, 2026-07-30).
/// Step two of MANDATORY authenticator enrolment: verifies the first code
/// against the staged secret, activates it, and issues the session the password
/// step withheld. The access token carries <c>amr=mfa</c>.
///
/// <para>Anonymous for the same reason as the start step — the enrolment ticket
/// is the credential and no token exists yet. On the reviewed allow-list in
/// <c>BusinessFlow13PermissionMatrixTests</c>.</para>
/// </summary>
public sealed class TwoFactorEnrolmentCompleteEndpoint(ISignInService signInService)
    : Endpoint<CompleteTwoFactorEnrolmentRequest, ApiResult<CompleteTwoFactorEnrolmentResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/enrolment/complete");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Confirm mandatory authenticator enrolment and complete the sign-in.");
    }

    public override async Task HandleAsync(
        CompleteTwoFactorEnrolmentRequest req, CancellationToken ct)
    {
        var response = await signInService.CompleteTwoFactorEnrolmentAsync(req, ct);
        await Send.OkAsync(ApiResult<CompleteTwoFactorEnrolmentResponse>.Ok(response), ct);
    }
}
