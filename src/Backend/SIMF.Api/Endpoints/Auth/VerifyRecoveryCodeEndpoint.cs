// Tests: SIMF.Api.Tests/RecoveryCodesTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// <c>POST /api/v1/auth/verify-recovery-code</c> — completes sign-in using a
/// single-use recovery code in place of an authenticator-app TOTP code
/// (decision D-040). Same MFA-token gate and same lockout budget as
/// <c>verify-totp</c>; the only difference is which secret the user proves.
/// </summary>
public sealed class VerifyRecoveryCodeEndpoint(ISignInService signInService)
    : Endpoint<VerifyRecoveryCodeRequest, ApiResult<AuthTokens>>
{
    public override void Configure()
    {
        Post("/auth/verify-recovery-code");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Complete sign-in with a single-use recovery code (fallback for a lost authenticator).");
    }

    public override async Task HandleAsync(
        VerifyRecoveryCodeRequest req, CancellationToken ct)
    {
        var tokens = await signInService.VerifyRecoveryCodeAsync(req, ct);
        await Send.OkAsync(ApiResult<AuthTokens>.Ok(tokens), ct);
    }
}
