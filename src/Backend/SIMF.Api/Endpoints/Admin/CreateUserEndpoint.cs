// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/staff</c> — an Administrator creates a new
/// Control Panel **staff** user (decision D-042; P3 renamed from
/// <c>/admin/users</c>). The new user is created in <c>Approved</c>
/// state with no password and receives a 7-day password-set invitation.
/// </summary>
public sealed class CreateStaffEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminCreateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/staff");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new Control Panel staff user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminCreateUserRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await adminAccountService.CreateStaffAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors</c> — a team member creates a new
/// **visitor** account (P3). The visitor signs in to the Website or the
/// Flutter app. Today still gated on the Administrator role; the wider
/// Team-only policy (Staff / Scientific / Security) lands in P4.
/// </summary>
public sealed class CreateVisitorEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminCreateVisitorRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new visitor account.");
    }

    public override async Task HandleAsync(AdminCreateVisitorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await adminAccountService.CreateVisitorAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(response), ct);
    }
}
