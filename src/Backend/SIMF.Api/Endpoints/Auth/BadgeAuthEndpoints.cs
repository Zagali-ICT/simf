// Tests: SIMF.Api.Tests/BadgeAuthTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>
/// Part B — <c>POST /api/v1/app/auth/resolve-badge</c>. The app posts a scanned
/// badge QR; the response tells it which path to take (normal sign-in vs.
/// set-password activation). Anonymous + rate-limited like the other
/// pre-authentication auth endpoints; the QR's ~60-bit entropy makes the
/// found/not-found signal a non-viable enumeration oracle.
/// </summary>
public sealed class ResolveBadgeEndpoint(IBadgeAuthService badgeAuthService)
    : Endpoint<ResolveBadgeRequest, ApiResult<ResolveBadgeResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/resolve-badge");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Resolve a scanned badge QR to the sign-in / activation branch.");
    }

    public override async Task HandleAsync(ResolveBadgeRequest req, CancellationToken ct)
    {
        var response = await badgeAuthService.ResolveAsync(req, ct);
        await Send.OkAsync(ApiResult<ResolveBadgeResponse>.Ok(response), ct);
    }
}

/// <summary>
/// Part B — <c>POST /api/v1/app/auth/badge-sign-in</c>. A returning holder scans
/// their badge and completes sign-in with only their password; the QR selects
/// the account and the password (plus any 2FA / lockout) runs through the normal
/// sign-in pipeline. Returns the standard <see cref="SignInResponse"/> — the
/// issued tokens or the 2FA challenge, identical to email sign-in. Anonymous +
/// both auth rate-limits (mirrors the email sign-in); the QR's ~60-bit entropy
/// plus the generic-invalid response make it a non-viable enumeration oracle.
/// </summary>
public sealed class BadgeSignInEndpoint(IBadgeAuthService badgeAuthService)
    : Endpoint<BadgeSignInRequest, ApiResult<SignInResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/badge-sign-in");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Summary(summary => summary.Summary =
            "Complete a badge sign-in with the account password (returning holders).");
    }

    public override async Task HandleAsync(BadgeSignInRequest req, CancellationToken ct)
    {
        var response = await badgeAuthService.SignInAsync(req, ct);
        await Send.OkAsync(ApiResult<SignInResponse>.Ok(response), ct);
    }
}

/// <summary>
/// Part B — <c>POST /api/v1/app/auth/badge-activation/start</c>. Emails the
/// verification code that gates the first-password step for a passwordless
/// account. Anonymous + the email rate-limit (mirrors forgot-password).
/// </summary>
public sealed class BadgeActivationStartEndpoint(IBadgeAuthService badgeAuthService)
    : Endpoint<BadgeActivationStartRequest, ApiResult<BadgeActivationStartResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/badge-activation/start");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Summary(summary => summary.Summary =
            "Email a verification code to begin badge-account activation.");
    }

    public override async Task HandleAsync(BadgeActivationStartRequest req, CancellationToken ct)
    {
        var response = await badgeAuthService.StartActivationAsync(req, ct);
        await Send.OkAsync(ApiResult<BadgeActivationStartResponse>.Ok(response), ct);
    }
}

/// <summary>
/// Part B — <c>POST /api/v1/app/auth/badge-activation/complete</c>. Verifies the
/// emailed code and sets the account's first password. Anonymous + rate-limited.
/// </summary>
public sealed class BadgeActivationCompleteEndpoint(IBadgeAuthService badgeAuthService)
    : Endpoint<BadgeActivationCompleteRequest, ApiResult<BadgeActivationCompleteResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/badge-activation/complete");
        AllowAnonymous();
        Tags("Authentication");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Verify the code and set the first password for a badge account.");
    }

    public override async Task HandleAsync(BadgeActivationCompleteRequest req, CancellationToken ct)
    {
        var response = await badgeAuthService.CompleteActivationAsync(req, ct);
        await Send.OkAsync(ApiResult<BadgeActivationCompleteResponse>.Ok(response), ct);
    }
}
