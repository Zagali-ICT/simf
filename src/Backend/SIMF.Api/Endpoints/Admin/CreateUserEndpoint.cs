// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users</c> — an Administrator creates a new Control
/// Panel user account (decision D-042). The new user is created in the
/// <c>Approved</c> state with no password and receives an invitation email
/// carrying a 7-day password-set code.
/// </summary>
public sealed class CreateUserEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminCreateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/users");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new Control Panel user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminCreateUserRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var response = await adminAccountService.CreateUserAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(response), ct);
    }
}
