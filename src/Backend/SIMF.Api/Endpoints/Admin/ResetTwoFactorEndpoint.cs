// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/reset-two-factor</c> — an Administrator resets
/// another user's 2FA (decision D-041). Requires the Administrator role; the
/// target cannot be the actor or another Administrator. Audits both sides
/// with a mandatory reason.
/// </summary>
public sealed class ResetTwoFactorEndpoint(IAdminTwoFactorService adminAccountService)
    : Endpoint<AdminResetTwoFactorRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/admins/reset-two-factor");
        // No AllowAnonymous — the caller must be authenticated and hold the
        // Administrator role.
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.ResetTwoFactor), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Reset another user's two-factor authentication. Requires Administrator role.");
    }

    public override async Task HandleAsync(
        AdminResetTwoFactorRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();

        await adminAccountService.ResetTwoFactorAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>The named authorization policies the API uses.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Requires the caller to hold the Administrator role. Per the P7
    /// model (decision D-048) only <see cref="SIMF.Domain.IdentityAccess.UserType.Admin"/>
    /// users carry RBAC roles at all, so this is **the** policy every
    /// CP endpoint uses today. The P4-era <c>TeamMember</c> policy was
    /// removed by P7b when the reviewer roles (Staff / Scientific /
    /// Security) ceased to be RBAC roles.
    /// </summary>
    public const string AdministratorOnly = "AdministratorOnly";

    /// <summary>
    /// Requires the caller's <c>account_state</c> JWT claim (P10 — D-051)
    /// to be <c>"Approved"</c>. Defense-in-depth gate available to any
    /// endpoint that should not be reachable by a non-approved (guest)
    /// user. Today (P11 — D-052) no endpoint applies this policy by
    /// default — the client-side routing on CP + Website is the primary
    /// gate. A follow-up sweep will opt sensitive endpoints into this
    /// policy explicitly.
    /// </summary>
    public const string RequireApprovedAccount = "RequireApprovedAccount";

    /// <summary>D-148 — admin-only management of gates / assignments /
    /// allow-lists / reports (SIMF-API-GATES-001 §4).</summary>
    public const string GatesManage = "GatesManage";

    /// <summary>D-148 — operator-only scan submission and own-assignments
    /// listing (SIMF-API-GATES-001 §4). Administrator inherits.</summary>
    public const string GatesOperate = "GatesOperate";

    /// <summary>D-148 — operator's own-daily-report endpoint.</summary>
    public const string GatesViewOwnReports = "GatesViewOwnReports";

    /// <summary>D-168 (gap doc G5) — Administrator or
    /// <see cref="SIMF.Common.AppRoles.PublicRelations"/>. Used by every
    /// invitation CRUD endpoint and the VIP list + notify endpoints.
    /// PublicRelations cannot reach any other admin surface; the policy
    /// keeps the two roles distinct.</summary>
    public const string PublicRelationsAccess = "PublicRelationsAccess";

    /// <summary>Registers the policies with the ASP.NET Core authorization stack.</summary>
    public static void AddSimfAuthorization(this AuthorizationBuilder builder)
    {
        builder.AddPolicy(AdministratorOnly, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator));

        // P11 — D-052: gate by the account_state claim minted by
        // JwtTokenService (P10). Non-approved users see the state-banner
        // page on the client; this policy is the API's matching guard
        // for any endpoint that opts in.
        builder.AddPolicy(RequireApprovedAccount, policy =>
            policy.RequireClaim("account_state", "Approved"));

        // D-148 — Administrator bypasses the gate permission checks
        // (an admin can also operate a gate from the CP console for
        // testing); GateOperator holds the operator permissions only.
        builder.AddPolicy(GatesManage, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator));
        builder.AddPolicy(GatesOperate, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator,
                               SIMF.Common.AppRoles.GateOperator));
        builder.AddPolicy(GatesViewOwnReports, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator,
                               SIMF.Common.AppRoles.GateOperator));

        // D-168 (gap doc G5) — PublicRelations role gate. Administrator
        // inherits so an admin can also operate the invitation desk from
        // the CP console.
        builder.AddPolicy(PublicRelationsAccess, policy =>
            policy.RequireRole(SIMF.Common.AppRoles.Administrator,
                               SIMF.Common.AppRoles.PublicRelations));
    }
}
