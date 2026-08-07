// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
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
        var created = await adminAccountService.DuplicateUserAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(created), ct);
    }
}
