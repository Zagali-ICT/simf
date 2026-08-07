// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/visitors/{id:guid}/reject</c> — set a pending
/// visitor to Rejected with a mandatory free-text reason (P4). Any CP role.
/// </summary>
public sealed class RejectVisitorEndpoint(IAdminUserApprovalService adminAccountService)
    : Endpoint<RejectRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/reject");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Reject), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Reject a pending visitor. Requires the Visitors.Reject permission.");
    }

    public override async Task HandleAsync(RejectRouteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await adminAccountService.RejectVisitorAsync(actorId, req.Id,
            new AdminRejectRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
