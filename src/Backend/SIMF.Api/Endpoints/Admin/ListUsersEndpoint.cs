// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/staff/list</c> — one page of staff accounts
/// (P3 renamed from <c>/admin/users/list</c>). Body is a
/// <see cref="GridQuery"/>; response is a <see cref="GridPage{TItem}"/>
/// of <see cref="AdminUserSummary"/>. Administrator role required.
///
/// <para>"Staff" = users with at least one CP role (Administrator today;
/// Staff / Scientific / Security from P4).</para>
/// </summary>
public sealed class ListStaffEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/staff/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of staff accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListStaffAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/list</c> — one page of visitor accounts
/// (P3). Visitor = user without any CP role.
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
            "Return one page of visitor accounts.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListVisitorsAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}
