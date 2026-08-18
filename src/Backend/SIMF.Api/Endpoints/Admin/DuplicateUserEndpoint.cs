// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
// Tests: SIMF.Api.Tests/DuplicateUserRoleGrantTests.cs (Admins.Create alone must
//        not duplicate a role-holding account onto a new address)
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/duplicate</c> — creates a copy of an existing
/// user with a new email and a fresh invite.
/// </summary>
public sealed class DuplicateUserEndpoint(IAdminUserProvisioningService adminAccountService)
    : Endpoint<AdminDuplicateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/duplicate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Duplicate an existing user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminDuplicateUserRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        // Email-shape rules live in AdminDuplicateUserRequestValidator.
        //
        // The copy inherits the source's roles, so duplicating a role-holding
        // account grants those roles - duplicating an Administrator hands the copy
        // the wildcard. That is the same act CreateAdminEndpoint refuses when the
        // payload carries a non-empty Roles list, so it is gated on the same
        // permission; the service refuses before creating anything when the flag is
        // false and the source holds roles. A role-less source still duplicates on
        // Admins.Create alone.
        var created = await adminAccountService.DuplicateUserAsync(
            actorId, req, CanAssignRoles(), ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(created), ct);
    }

    private bool CanAssignRoles() =>
        User.HasClaim(PermissionCatalog.ClaimType, PermissionCatalog.Wildcard)
        || User.HasClaim(PermissionCatalog.ClaimType, PermissionCatalog.Admins.AssignRoles);
}
