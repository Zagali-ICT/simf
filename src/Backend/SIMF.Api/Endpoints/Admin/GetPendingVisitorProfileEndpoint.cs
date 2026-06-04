// Tests: SIMF.Api.Tests/PendingProfileReadTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-124 — <c>GET /api/v1/admin/visitors/{id}/profile-for-approval</c>.
/// Returns the full <see cref="PendingProfileResponse"/> when the target
/// is a Visitor currently in PendingApproval; returns 404 otherwise
/// (unknown id, wrong state, or wrong UserType all collapse to one
/// response so an admin cannot enumerate via error-code diff). The CP
/// approve / reject flow renders this body in a preview modal before
/// the admin commits.
/// </summary>
public sealed class GetPendingVisitorProfileRequest
{
    public Guid Id { get; set; }
}

public sealed class GetPendingVisitorProfileEndpoint(IAdminApprovalReadService service)
    : Endpoint<GetPendingVisitorProfileRequest, ApiResult<PendingProfileResponse>>
{
    public override void Configure()
    {
        Get("/admin/visitors/{id:guid}/profile-for-approval");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Read the full profile for a pending Visitor. Returns 404 for unknown / approved / wrong-type ids.");
    }

    public override async Task HandleAsync(
        GetPendingVisitorProfileRequest req, CancellationToken ct)
    {
        var profile = await service.GetPendingVisitorProfileAsync(req.Id, ct);
        if (profile is null)
        {
            // Single 404 for missing / wrong-state / wrong-type so an
            // enumerator cannot diff error codes (matches the type-
            // smuggling stance D-113 took on the bulk endpoints).
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "No pending visitor was found for this id.",
                "لم يتم العثور على زائر بانتظار الموافقة بهذا المعرّف.");
        }
        await Send.OkAsync(ApiResult<PendingProfileResponse>.Ok(profile), ct);
    }
}
