// Tests: SIMF.Api.Tests/AdminBulkRejectTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-209 — <c>POST /api/v1/admin/visitors/bulk-reject</c>. The reject
/// counterpart of <see cref="BulkApproveVisitorsEndpoint"/>: reject every
/// selected pending visitor in one request with a shared reason. Each subject
/// is rejected in its own step; per-subject failures are reported and do not
/// block the rest. Reason + ids are validated by
/// <c>AdminBulkRejectRequestValidator</c>.</summary>
public sealed class BulkRejectVisitorsEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkRejectRequest, ApiResult<AdminBulkRejectResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/bulk-reject");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Reject),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Bulk-reject a batch of pending visitors with a shared reason. Up to 500 ids per request.");
    }

    public override async Task HandleAsync(
        AdminBulkRejectRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.BulkRejectVisitorsAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkRejectResponse>.Ok(result), ct);
    }
}

/// <summary>D-209 — <c>POST /api/v1/admin/others/bulk-reject</c>. Same shape as
/// <see cref="BulkRejectVisitorsEndpoint"/> for the partner-side (Other)
/// pending list; gated by <c>Others.Reject</c>.</summary>
public sealed class BulkRejectOthersEndpoint(IAdminUserApprovalService service)
    : Endpoint<AdminBulkRejectRequest, ApiResult<AdminBulkRejectResponse>>
{
    public override void Configure()
    {
        Post("/admin/others/bulk-reject");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Reject),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Bulk-reject a batch of pending partner (Other) accounts with a shared reason. Up to 500 ids per request.");
    }

    public override async Task HandleAsync(
        AdminBulkRejectRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.BulkRejectOthersAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkRejectResponse>.Ok(result), ct);
    }
}
