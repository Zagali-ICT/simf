// Tests: SIMF.Api.Tests/AdminCreateUserTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/users</c> — returns every account in the system
/// (decision D-042). Bounded user count today, so no pagination yet — the
/// wider User Management module will add filter / search / paging.
/// </summary>
public sealed class ListUsersEndpoint(IAdminAccountService adminAccountService)
    : EndpointWithoutRequest<ApiResult<AdminUserListResponse>>
{
    public override void Configure()
    {
        Get("/admin/users");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List every account. Requires Administrator role.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await adminAccountService.ListUsersAsync(ct);
        await Send.OkAsync(ApiResult<AdminUserListResponse>.Ok(response), ct);
    }
}
