using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/app/auth/resend-otp</c> — #12: re-issue the emailed sign-in
/// one-time code for an in-progress visitor 2FA session (keyed by the OTP
/// ticket; no password needed). Lets the OTP screen "Resend" in place instead of
/// bouncing back to sign-in.
/// </summary>
public sealed class ResendOtpEndpoint(ISignInService signInService)
    : Endpoint<ResendOtpRequest, ApiResult<ResendOtpResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/resend-otp");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Re-issue the emailed sign-in code for an in-progress 2FA session.");
    }

    public override async Task HandleAsync(ResendOtpRequest req, CancellationToken ct)
    {
        var result = await signInService.ResendOtpAsync(req, ct);
        await Send.OkAsync(ApiResult<ResendOtpResponse>.Ok(result), ct);
    }
}
