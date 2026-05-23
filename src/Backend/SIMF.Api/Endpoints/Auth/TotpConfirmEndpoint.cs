// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/totp/confirm</c> — verifies the first authenticator
/// code against the staged secret; on success the secret becomes active and
/// <c>TwoFactorEnabled</c> is turned on. Requires a valid access token.
/// </summary>
public sealed class TotpConfirmEndpoint(ITotpEnrollmentService totpEnrollment)
    : Endpoint<TotpConfirmRequest, ApiResult<TotpConfirmResponse>>
{
    public override void Configure()
    {
        Post("/auth/totp/confirm");
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Confirm authenticator-app enrolment with a first valid code.");
    }

    public override async Task HandleAsync(TotpConfirmRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await totpEnrollment.ConfirmAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<TotpConfirmResponse>.Ok(response), ct);
    }
}
