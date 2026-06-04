// Tests: SIMF.Api.Tests/AdminProfileReadTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-126 — twin of <see cref="GetVisitorProfileEndpoint"/> for Other.</summary>
public sealed class GetOtherProfileRequest
{
    public Guid Id { get; set; }
}

public sealed class GetOtherProfileEndpoint(IAdminApprovalReadService service)
    : Endpoint<GetOtherProfileRequest, ApiResult<AdminUserProfileView>>
{
    public override void Configure()
    {
        Get("/admin/others/{id:guid}/profile");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return the full Other-account profile. 404 for unknown / wrong-type ids.");
    }

    public override async Task HandleAsync(GetOtherProfileRequest req, CancellationToken ct)
    {
        var view = await service.GetOtherProfileAsync(req.Id, ct);
        if (view is null)
        {
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "No Other account was found for this id.",
                "لم يتم العثور على حساب آخر بهذا المعرّف.");
        }
        await Send.OkAsync(ApiResult<AdminUserProfileView>.Ok(view), ct);
    }
}
