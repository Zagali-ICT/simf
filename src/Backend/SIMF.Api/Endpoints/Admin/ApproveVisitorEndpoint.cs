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
/// CS-D (D-386) — the body may optionally carry a
/// <see cref="ApproveVisitorRequest.ProfileTypeId"/> to set the visitor's
/// tier as part of the approval; null leaves the tier unchanged.
/// </summary>
public sealed class ApproveVisitorEndpoint(IAdminUserApprovalService adminAccountService)
    : Endpoint<ApproveVisitorRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Approve), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending visitor. Requires the Administrator role (P7b).");
    }

    public override async Task HandleAsync(ApproveVisitorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.ApproveVisitorAsync(actorId, req.Id, req.ProfileTypeId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>CS-D (D-386) — the approve-visitor request. <see cref="Id"/>
/// binds from the route; <see cref="ProfileTypeId"/> is an optional tier
/// from the JSON body (null = leave the visitor's tier unchanged).</summary>
public sealed class ApproveVisitorRequest
{
    public Guid Id { get; set; }
    public Guid? ProfileTypeId { get; set; }
}
