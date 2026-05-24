// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/staff/{id:guid}/approve</c> — flip a pending
/// staff account to Approved + mint the QR id (P4). Administrator-only.
/// </summary>
public sealed class ApproveStaffEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/staff/{id:guid}/approve");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending staff account. Requires Administrator role.");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.ApproveStaffAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Empty body — the user id comes from the route, the actor from the bearer.</summary>
public sealed class ApproveRouteRequest
{
    public Guid Id { get; set; }
}
