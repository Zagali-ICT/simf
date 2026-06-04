// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/totp/setup</c> — begins authenticator-app enrolment
/// (myComment item #11). Returns a fresh secret, the <c>otpauth://</c> URI and
/// an SVG QR code to render in the page. Requires a valid access token.
/// </summary>
public sealed class TotpSetupEndpoint(ITotpEnrollmentService totpEnrollment)
    : EndpointWithoutRequest<ApiResult<TotpSetupResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/setup");
        // No AllowAnonymous — FastEndpoints requires an authenticated caller.
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Begin authenticator-app enrolment (requires sign-in).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await totpEnrollment.SetupAsync(userId, ct);
        await Send.OkAsync(ApiResult<TotpSetupResponse>.Ok(response), ct);
    }
}
