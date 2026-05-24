// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/duplicate</c> — creates a copy of an existing
/// user with a new email and a fresh invite (decision D-044 b).
/// </summary>
public sealed class DuplicateUserEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminDuplicateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/duplicate");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Duplicate an existing user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminDuplicateUserRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        // Email-shape rules live in AdminDuplicateUserRequestValidator.
        var created = await adminAccountService.DuplicateUserAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(created), ct);
    }
}
