// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/totp/disable</c> — turns 2FA off after the user
/// proves a current authenticator code. Requires a valid access token.
/// </summary>
public sealed class TotpDisableEndpoint(ITotpEnrollmentService totpEnrollment)
    : Endpoint<TotpDisableRequest, ApiResult<TotpDisableResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/totp/disable");
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Turn two-factor authentication off (requires a current code).");
    }

    public override async Task HandleAsync(TotpDisableRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await totpEnrollment.DisableAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<TotpDisableResponse>.Ok(response), ct);
    }
}
