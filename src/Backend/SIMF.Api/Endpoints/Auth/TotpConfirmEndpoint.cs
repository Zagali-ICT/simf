// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/confirm</c> — verifies the first authenticator
/// code against the staged secret; on success the secret becomes active and
/// <c>TwoFactorEnabled</c> is turned on. Requires a valid access token.
/// </summary>
public sealed class TotpConfirmEndpoint(ITotpEnrollmentService totpEnrollment)
    : Endpoint<TotpConfirmRequest, ApiResult<TotpConfirmResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/confirm");
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Confirm authenticator-app enrolment with a first valid code.");
    }

    public override async Task HandleAsync(TotpConfirmRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await totpEnrollment.ConfirmAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<TotpConfirmResponse>.Ok(response), ct);
    }
}
