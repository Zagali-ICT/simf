// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/pending/list</c> — one page of pending
/// Admins (P4; P7c renamed URL from <c>/admin/staff/pending/list</c>).
/// Administrator-only.
/// </summary>
public sealed class ListPendingAdminsEndpoint(IAdminUserProvisioningService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminPendingUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/admins/pending/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List pending Admins. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListPendingAdminsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/pending/list</c> — one page of pending
/// Others (P7c — new). Administrator-only.
/// </summary>
public sealed class ListPendingOthersEndpoint(IAdminUserProvisioningService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminPendingUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/others/pending/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List pending Others. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListPendingOthersAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/pending/list</c> — one page of pending
/// Visitors (P4).
/// </summary>
public sealed class ListPendingVisitorsEndpoint(IAdminUserProvisioningService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminPendingUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/visitors/pending/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List pending visitors. Requires the Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListPendingVisitorsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page), ct);
    }
}
