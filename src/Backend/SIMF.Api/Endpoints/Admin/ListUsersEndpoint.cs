// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/list</c> — one page of Admin accounts
/// (P7c — renamed from <c>/admin/staff/list</c>). Administrator-only.
/// </summary>
public sealed class ListAdminsEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/admins/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of Admin accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListAdminsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/list</c> — one page of Other accounts
/// (P7c — new). Administrator-only.
/// </summary>
public sealed class ListOthersEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/others/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of Other accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListOthersAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/list</c> — one page of Visitor
/// accounts (P3; P7c rekeyed off UserType).
/// </summary>
public sealed class ListVisitorsEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/visitors/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of visitor accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListVisitorsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}
