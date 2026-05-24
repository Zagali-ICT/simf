// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/staff/{id:guid}/reject</c> — set a pending staff
/// account to Rejected with a mandatory free-text reason (P4).
/// Administrator-only.
/// </summary>
public sealed class RejectStaffEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<RejectRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/staff/{id:guid}/reject");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Reject a pending staff account. Requires Administrator role.");
    }

    public override async Task HandleAsync(RejectRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await adminAccountService.RejectStaffAsync(actorId, req.Id,
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
