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
        Post("/admin/users/bulk-delete");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
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
        if (req.Ids.Count == 0)
        {
            throw new DataValidationException(
                "At least one user id is required.",
                "يجب تحديد مستخدم واحد على الأقل.");
        }
        if (req.Reason.Length is < 10 or > 500)
        {
            throw new DataValidationException(
                "A reason between 10 and 500 characters is required.",
                "السبب مطلوب وبين 10 و 500 حرفًا.");
        }
        var response = await adminAccountService.BulkDeleteUsersAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkDeleteResponse>.Ok(response), ct);
    }
}
