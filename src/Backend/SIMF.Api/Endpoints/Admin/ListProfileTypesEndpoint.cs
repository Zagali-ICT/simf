// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;

using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/profile-types?userType=Other</c> — every active
/// <c>ProfileType</c> row for the given <see cref="UserType"/>. Drives the
/// CP create page's subtype dropdown. Requires the ProfileTypes.View permission.
/// </summary>
public sealed class ListProfileTypesRequest
{
    public string UserType { get; set; } = string.Empty;
}

public sealed class ListProfileTypesEndpoint(IAdminProfileTypeQueryService adminAccountService)
    : Endpoint<ListProfileTypesRequest, ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>
{
    public override void Configure()
    {
        Get("/admin/profile-types");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProfileTypes.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
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
