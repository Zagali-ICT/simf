// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/list</c> — one page of accounts (decisions
/// D-042, D-044). Body is a <see cref="GridQuery"/>; response is a
/// <see cref="GridPage{TItem}"/> of <see cref="AdminUserSummary"/>.
/// Administrator role required.
///
/// <para>POST (not GET) so the search / sort / filter parameters travel in
/// the body — they can be long and structured, and GET-with-querystring
/// runs into URL length + URL-encoding pain for the per-column filters.</para>
/// </summary>
public sealed class ListUsersEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminUserSummary>>>
{
    public override void Configure()
    {
        Post("/admin/users/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await adminAccountService.ListUsersAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminUserSummary>>.Ok(page), ct);
    }
}
