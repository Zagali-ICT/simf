// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/bulk-delete</c> — soft-deletes one or many
/// users. Self / Administrator targets are silently
/// skipped per-target so the batch never fails on a single guarded row.
/// One audit row per subject so the SOC sees every deletion.
/// </summary>
public sealed class BulkDeleteUsersEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminBulkDeleteRequest, ApiResult<AdminBulkDeleteResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/bulk-delete");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Soft-delete one or many users. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminBulkDeleteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        // Field-level rules (Ids cap, Reason 10-500) live in
        // AdminBulkDeleteRequestValidator so the standard VALIDATION_FAILED
        // envelope is the response shape.
        var response = await adminAccountService.BulkDeleteUsersAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkDeleteResponse>.Ok(response), ct);
    }
}
