// Tests: SIMF.Api.Tests/AdminProfileReadTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/visitors/{id}/profile</c>:
/// returns the full <see cref="AdminUserProfileView"/> for any visitor
/// in any account state. A type-match guard keeps it to visitors (a
/// non-Visitor id 404s). Every successful read triggers a row-audit row via
/// the SaveChanges interceptor when the SimfUser row materialises.
/// </summary>
public sealed class GetVisitorProfileRequest
{
    public Guid Id { get; set; }
}

public sealed class GetVisitorProfileEndpoint(IAdminApprovalReadService service)
    : Endpoint<GetVisitorProfileRequest, ApiResult<AdminUserProfileView>>
{
    public override void Configure()
    {
        Get("/admin/visitors/{id:guid}/profile");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return the full visitor profile. 404 for unknown / wrong-type ids.");
    }

    public override async Task HandleAsync(GetVisitorProfileRequest req, CancellationToken ct)
    {
        var view = await service.GetVisitorProfileAsync(req.Id, ct);
        if (view is null)
        {
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "No visitor was found for this id.",
                "لم يتم العثور على زائر بهذا المعرّف.");
        }
        await Send.OkAsync(ApiResult<AdminUserProfileView>.Ok(view), ct);
    }
}
