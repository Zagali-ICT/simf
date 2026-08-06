// Tests: SIMF.Api.Tests/PendingProfileReadTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/others/{id}/profile-for-approval</c>.
/// Twin of <see cref="GetPendingVisitorProfileEndpoint"/>, restricted to
/// <c>UserType = Other</c>. Same 404-collapses-all-mismatch policy.
/// </summary>
public sealed class GetPendingOtherProfileRequest
{
    public Guid Id { get; set; }
}

public sealed class GetPendingOtherProfileEndpoint(IAdminApprovalReadService service)
    : Endpoint<GetPendingOtherProfileRequest, ApiResult<PendingProfileResponse>>
{
    public override void Configure()
    {
        Get("/admin/others/{id:guid}/profile-for-approval");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Read the full profile for a pending Other account. Returns 404 for unknown / approved / wrong-type ids.");
    }

    public override async Task HandleAsync(
        GetPendingOtherProfileRequest req, CancellationToken ct)
    {
        var profile = await service.GetPendingOtherProfileAsync(req.Id, ct);
        if (profile is null)
        {
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "No pending Other account was found for this id.",
                "لم يتم العثور على حساب آخر بانتظار الموافقة بهذا المعرّف.");
        }
        await Send.OkAsync(ApiResult<PendingProfileResponse>.Ok(profile), ct);
    }
}
