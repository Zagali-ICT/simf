// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/reset-two-factor</c> — an Administrator resets
/// another user's 2FA (decision D-041). Requires the Administrator role; the
/// target cannot be the actor or another Administrator. Audits both sides
/// with a mandatory reason.
/// </summary>
public sealed class ResetTwoFactorEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminResetTwoFactorRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/staff/reset-two-factor");
        // No AllowAnonymous — the caller must be authenticated and hold the
        // Administrator role.
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Reset another user's two-factor authentication. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminResetTwoFactorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await adminAccountService.ResetTwoFactorAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>The named authorization policies the API uses.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires the caller to hold the Administrator role.</summary>
    public const string AdministratorOnly = "AdministratorOnly";

    /// <summary>
    /// Requires the caller to hold any CP role — Administrator, Staff,
    /// Scientific or Security (P4). Used by visitor-approval endpoints
    /// (any team member can approve any visitor).
    /// </summary>
    public const string TeamMember = "TeamMember";

    /// <summary>Registers the policies with the ASP.NET Core authorization stack.</summary>
    public static void AddSimfAuthorization(this AuthorizationBuilder builder)
    {
        builder.AddPolicy(AdministratorOnly, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator));
        builder.AddPolicy(TeamMember, policy =>
            policy.RequireRole([.. SIMF.Common.AppRoles.CpRoles]));
    }
}
