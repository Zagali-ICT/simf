// Tests: SIMF.Api.Tests/DelegatesAndBulkBadgesTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/visitors/badge-batches/list</c>. The
/// server-paged list of persisted bulk-badge batches (each a
/// <see cref="BulkGenerateVisitorBadgesEndpoint"/> run), newest first. A revoked batch
/// keeps its row with <c>IsActive = false</c>.
/// </summary>
public sealed class ListBadgeBatchesEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminBadgeBatchSummary>>>
{
    public override void Configure()
    {
        Post("/admin/visitors/badge-batches/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "List the persisted bulk-badge batches. Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var page = await adminAccountService.ListBadgeBatchesAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/badge-batches/re-email</c>.
/// Re-renders the batch's QR pack and emails a fresh copy to the supplied organiser
/// address; the badges themselves are unchanged.
/// </summary>
public sealed class ReEmailBadgeBatchEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminReEmailBadgeBatchRequest, ApiResult<AdminReEmailBadgeBatchResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/badge-batches/re-email");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ManageBatches), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Re-email a bulk-badge batch's QR pack to an organiser. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminReEmailBadgeBatchRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var response = await adminAccountService.ReEmailBadgeBatchAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminReEmailBadgeBatchResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/badge-batches/revoke</c>.
/// Disables every account the batch minted (reusing the type-scoped bulk-delete path)
/// and marks the batch inactive. Not reversible.
/// </summary>
public sealed class RevokeBadgeBatchEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminRevokeBadgeBatchRequest, ApiResult<AdminRevokeBadgeBatchResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/badge-batches/revoke");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ManageBatches), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Revoke a bulk-badge batch (disable its accounts). Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminRevokeBadgeBatchRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var response = await adminAccountService.RevokeBadgeBatchAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminRevokeBadgeBatchResponse>.Ok(response), ct);
    }
}
