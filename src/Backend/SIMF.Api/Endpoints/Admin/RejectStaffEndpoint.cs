// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/{id:guid}/reject</c> — set a pending
/// Admin to Rejected with a mandatory 10–500 char reason (P4; P7c
/// renamed URL from <c>/admin/staff</c>). Administrator-only.
/// </summary>
public sealed class RejectAdminEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<RejectRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/admins/{id:guid}/reject");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Reject a pending Admin. Requires Administrator role.");
    }

    public override async Task HandleAsync(RejectRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.RejectAdminAsync(actorId, req.Id,
            new AdminRejectRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/{id:guid}/reject</c> — set a pending
/// Other to Rejected with a mandatory 10–500 char reason (P7c — new).
/// Administrator-only.
/// </summary>
public sealed class RejectOtherEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<RejectRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/others/{id:guid}/reject");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Reject a pending Other. Requires Administrator role.");
    }

    public override async Task HandleAsync(RejectRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.RejectOtherAsync(actorId, req.Id,
            new AdminRejectRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Route id + reason body. FastEndpoints merges route + body for us.</summary>
public sealed class RejectRouteRequest
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
}
