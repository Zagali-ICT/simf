// Tests: SIMF.Api.Tests/AdminBulkApprovalTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-164 (PDF §2.7.1, gap doc G2) — <c>POST /api/v1/admin/visitors/bulk-approve</c>.
/// The security team's "Select All" affordance. Each subject is approved in
/// its own step; per-subject failures are reported in the response and do
/// not block the rest. One audit row + one operation-log row per subject.</summary>
public sealed class BulkApproveVisitorsEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkApprovalRequest, ApiResult<AdminBulkApprovalResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/bulk-approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Bulk-approve a batch of pending visitors. Up to 500 ids per request.");
    }

    public override async Task HandleAsync(
        AdminBulkApprovalRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        if (req.Ids is null || req.Ids.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminBulkActionInvalid, 400,
                "At least one user id is required.",
                "يجب تحديد مستخدم واحد على الأقل.");
        }
        var result = await service.BulkApproveVisitorsAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkApprovalResponse>.Ok(result), ct);
    }
}

/// <summary>D-164 — <c>POST /api/v1/admin/others/bulk-approve</c>. Same
/// shape as <see cref="BulkApproveVisitorsEndpoint"/> for the Other-tier
/// pending list.</summary>
public sealed class BulkApproveOthersEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkApprovalRequest, ApiResult<AdminBulkApprovalResponse>>
{
    public override void Configure()
    {
        // P1.3 (D-214) security fix — was gated on Visitors.Approve, which let
        // an admin holding only visitor-approve rights bulk-approve PARTNER
        // (Other) accounts. The partner queue must gate on Others.Approve, the
        // same code the single ApproveOther endpoint uses.
        Post("/admin/others/bulk-approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        AdminBulkApprovalRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        if (req.Ids is null || req.Ids.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminBulkActionInvalid, 400,
                "At least one user id is required.",
                "يجب تحديد مستخدم واحد على الأقل.");
        }
        var result = await service.BulkApproveOthersAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkApprovalResponse>.Ok(result), ct);
    }
}
