// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/staff/pending/list</c> — one page of pending
/// staff accounts (P4). Administrator-only.
/// </summary>
public sealed class ListPendingStaffEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminPendingUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/staff/pending/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List pending staff accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListPendingStaffAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/pending/list</c> — one page of pending
/// visitors (P4). Any CP role.
/// </summary>
public sealed class ListPendingVisitorsEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminPendingUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/visitors/pending/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List pending visitors. Requires the Administrator role (P7b).");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListPendingVisitorsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page), ct);
    }
}
