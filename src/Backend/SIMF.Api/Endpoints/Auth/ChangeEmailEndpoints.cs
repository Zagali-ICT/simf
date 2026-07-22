// Tests: SIMF.Api.Tests/ChangeEmailTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>#24 — <c>POST /api/v1/app/auth/change-email/send-otp</c>: a signed-in
/// user asks to move their login email to a new address; the server emails a
/// one-time code TO that new address. Approved-account only; the per-account
/// request cap lives in the service, "auth" adds the per-IP limiter.</summary>
public sealed class SendEmailChangeOtpEndpoint(IEmailChangeService service)
    : Endpoint<SendEmailChangeOtpRequest, ApiResult<SendEmailChangeOtpResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/change-email/send-otp");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Tags("Authentication");
        Summary(summary => summary.Summary =
            "Request a code to confirm a new email address (requires sign-in).");
    }

    public override async Task HandleAsync(SendEmailChangeOtpRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await service.SendChangeOtpAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<SendEmailChangeOtpResponse>.Ok(response), ct);
    }
}

/// <summary>#24 — <c>POST /api/v1/app/auth/change-email/confirm</c>: the signed-in
/// user submits the code emailed to the new address to complete the change. On
/// success the login email moves, the security stamp is rolled and every session
/// is revoked, so the caller's token is dead and the app re-authenticates.</summary>
public sealed class ConfirmEmailChangeEndpoint(IEmailChangeService service)
    : Endpoint<ConfirmEmailChangeRequest, ApiResult<ConfirmEmailChangeResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/change-email/confirm");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(routeBuilder => routeBuilder
            .RequireRateLimiting("auth")
            .RequireRateLimiting("auth-email"));
        Tags("Authentication");
        Summary(summary => summary.Summary =
            "Confirm a new email address with the emailed code (requires sign-in).");
    }

    public override async Task HandleAsync(ConfirmEmailChangeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await service.ConfirmChangeAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<ConfirmEmailChangeResponse>.Ok(response), ct);
    }
}
