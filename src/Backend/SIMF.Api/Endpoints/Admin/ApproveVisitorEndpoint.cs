// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/visitors/{id:guid}/approve</c> — flip a pending
/// visitor to Approved + mint the QR id (P4). Any CP role may call.
/// </summary>
public sealed class ApproveVisitorEndpoint(IAdminUserApprovalService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Approve), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending visitor. Requires the Administrator role (P7b).");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.ApproveVisitorAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
