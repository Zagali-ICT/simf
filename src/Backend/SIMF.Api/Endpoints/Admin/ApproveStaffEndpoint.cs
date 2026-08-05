// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/{id:guid}/approve</c> — flip a pending
/// Admin to Approved + mint the QR id (P4; P7c renamed URL from
/// <c>/admin/staff</c>). Administrator-only.
/// </summary>
public sealed class ApproveAdminEndpoint(IAdminUserApprovalService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/admins/{id:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Approve), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending Admin. Requires Administrator role.");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await adminAccountService.ApproveAdminAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/{id:guid}/approve</c> — flip a pending
/// Other to Approved + mint the QR id (P7c — new).
/// <para>D-834 — gated on <c>Others.Approve</c>, the code for the subject
/// being acted on. It was copy-pasted from <see cref="ApproveAdminEndpoint"/>
/// above with the admin policy line left in, so approving a PARTNER account
/// demanded the code that exists to approve ADMINS. That contradicted
/// SIMF-Permission-Catalogue §Others, which has always mapped
/// <c>Others.Approve</c> to both <c>…/{id}/approve</c> and
/// <c>…/bulk-approve</c>; D-214 fixed the bulk half and recorded that it now
/// matched "the same code the single ApproveOther endpoint uses", which was
/// not yet true. The two halves of one action agree from here.</para>
/// </summary>
public sealed class ApproveOtherEndpoint(IAdminUserApprovalService adminAccountService)
    : Endpoint<ApproveRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/others/{id:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Approve), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Approve a pending Other. Requires Administrator role.");
    }

    public override async Task HandleAsync(ApproveRouteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await adminAccountService.ApproveOtherAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Empty body — the user id comes from the route, the actor from the bearer.</summary>
public sealed class ApproveRouteRequest
{
    public Guid Id { get; set; }
}
