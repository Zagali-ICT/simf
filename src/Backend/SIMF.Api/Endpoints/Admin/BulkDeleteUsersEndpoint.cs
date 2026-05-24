// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/bulk-delete</c> — soft-deletes one or many
/// users (decision D-044 b). Self / Administrator targets are silently
/// skipped per-target so the batch never fails on a single guarded row.
/// One audit row per subject so the SOC sees every deletion.
/// </summary>
public sealed class BulkDeleteUsersEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminBulkDeleteRequest, ApiResult<AdminBulkDeleteResponse>>
{
    public override void Configure()
    {
        Post("/admin/admins/bulk-delete");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Soft-delete one or many users. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminBulkDeleteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        // Field-level rules (Ids cap, Reason 10-500) live in
        // AdminBulkDeleteRequestValidator so the standard VALIDATION_FAILED
        // envelope (D-030) is the response shape.
        var response = await adminAccountService.BulkDeleteUsersAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkDeleteResponse>.Ok(response), ct);
    }
}
