// Tests: SIMF.Api.Tests/AdminBulkAdminTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>P1.3 (D-214) — <c>POST /api/v1/admin/admins/bulk-approve</c>. The
/// admin-queue counterpart of <see cref="BulkApproveVisitorsEndpoint"/>:
/// bulk-approve a batch of pending admin (staff) accounts. Each subject is
/// approved in its own step; per-subject failures are reported and do not block
/// the rest. Gated by <c>Admins.Approve</c> (the same code the single
/// ApproveStaff endpoint uses).</summary>
public sealed class BulkApproveAdminsEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkApprovalRequest, ApiResult<AdminBulkApprovalResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/bulk-approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Bulk-approve a batch of pending admins. Up to 500 ids per request.");
    }

    public override async Task HandleAsync(
        AdminBulkApprovalRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        if (req.Ids is null || req.Ids.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminBulkActionInvalid, 400,
                "At least one user id is required.",
                "يجب تحديد مستخدم واحد على الأقل.");
        }
        var result = await service.BulkApproveAdminsAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkApprovalResponse>.Ok(result), ct);
    }
}

/// <summary>P1.3 (D-214) — <c>POST /api/v1/admin/admins/bulk-reject</c>. The
/// reject counterpart of <see cref="BulkApproveAdminsEndpoint"/>: bulk-reject
/// the selected pending admins with a shared reason. Ids + reason are validated
/// by <c>AdminBulkRejectRequestValidator</c>. Gated by <c>Admins.Reject</c>.</summary>
public sealed class BulkRejectAdminsEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkRejectRequest, ApiResult<AdminBulkRejectResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/bulk-reject");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Reject),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Bulk-reject a batch of pending admins with a shared reason. Up to 500 ids per request.");
    }

    public override async Task HandleAsync(
        AdminBulkRejectRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var result = await service.BulkRejectAdminsAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkRejectResponse>.Ok(result), ct);
    }
}
