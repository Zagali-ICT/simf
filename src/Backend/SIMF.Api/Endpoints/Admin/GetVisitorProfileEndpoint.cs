// Tests: SIMF.Api.Tests/AdminProfileReadTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-126 — <c>GET /api/v1/admin/visitors/{id}/profile</c>. Q-G reversed:
/// returns the full <see cref="AdminUserProfileView"/> for any visitor
/// in any account state. Type-match guard preserved (a non-Visitor id
/// 404s). Every successful read triggers a D-109 row-audit row via the
/// SaveChanges interceptor when the SimfUser row materialises.
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
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
