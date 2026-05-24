// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/{id:guid}/approve</c> — flip a pending
/// Admin to Approved + mint the QR id (P4; P7c renamed URL from
/// <c>/admin/staff</c>). Administrator-only.
/// </summary>
public sealed class ApproveAdminEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/admins/{id:guid}/approve");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending Admin. Requires Administrator role.");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.ApproveAdminAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/{id:guid}/approve</c> — flip a pending
/// Other to Approved + mint the QR id (P7c — new). Administrator-only.
/// </summary>
public sealed class ApproveOtherEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/others/{id:guid}/approve");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending Other. Requires Administrator role.");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.ApproveOtherAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Empty body — the user id comes from the route, the actor from the bearer.</summary>
public sealed class ApproveRouteRequest
{
    public Guid Id { get; set; }
}
