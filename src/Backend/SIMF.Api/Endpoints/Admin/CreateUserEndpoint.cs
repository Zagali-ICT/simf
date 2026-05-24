// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs, SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins</c> — an Administrator creates a new
/// Control Panel **Admin** user (P7c — renamed from
/// <c>POST /admin/staff</c>; decisions D-042 + D-048). The new account
/// lands in <c>PendingApproval</c> with no password and receives a
/// 7-day password-set invitation. Approval is Administrator-only and
/// mints the QR id (D-046a + P4).
/// </summary>
public sealed class CreateAdminEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminCreateAdminRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new Control Panel Admin user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminCreateAdminRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await adminAccountService.CreateAdminAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others</c> — Administrator creates a new
/// <b>Other</b> user (P7c — new). The new user signs in to the Flutter
/// app, not the CP. The <c>ProfileTypeId</c> picks the Other subtype
/// from the <c>ProfileTypes</c> lookup.
/// </summary>
public sealed class CreateOtherEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminCreateOtherRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/others");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create a new Other user. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminCreateOtherRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await adminAccountService.CreateOtherAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors</c> — Administrator creates a new
/// <b>Visitor</b> user (P3; P7c added optional <c>ProfileTypeId</c>).
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
