// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/visitors/{id:guid}/reject</c> — set a pending
/// visitor to Rejected with a mandatory free-text reason (P4). Any CP role.
/// </summary>
public sealed class RejectVisitorEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<RejectRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/reject");
        Policies(nameof(AuthorizationPolicies.TeamMember));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Reject a pending visitor. Requires any CP role.");
    }

    public override async Task HandleAsync(RejectRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.RejectVisitorAsync(actorId, req.Id,
            new AdminRejectRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
