// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/profile-types?userType=Other</c> — every active
/// <c>ProfileType</c> row for the given <see cref="UserType"/>. Drives the
/// CP create page's subtype dropdown (P7c — D-048). Administrator-only.
/// </summary>
public sealed class ListProfileTypesRequest
{
    public string UserType { get; set; } = string.Empty;
}

public sealed class ListProfileTypesEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<ListProfileTypesRequest, ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>
{
    public override void Configure()
    {
        Get("/admin/profile-types");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return active ProfileTypes filtered by UserType. Requires Administrator role.");
    }

    public override async Task HandleAsync(ListProfileTypesRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<UserType>(req.UserType, ignoreCase: true, out var userType))
        {
            await Send.OkAsync(
                ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                    Array.Empty<AdminProfileTypeSummary>()),
                ct);
            return;
        }
        var rows = await adminAccountService.ListProfileTypesAsync(userType, ct);
        await Send.OkAsync(
            ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(rows), ct);
    }
}
