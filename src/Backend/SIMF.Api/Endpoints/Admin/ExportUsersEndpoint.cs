// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/export</c> — returns an XLSX workbook of the
/// selected users (or the whole grid result if no ids are given).
/// Decision D-044 b.
/// </summary>
public sealed class ExportUsersEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<AdminExportUsersRequest>
{
    public override void Configure()
    {
        Post("/admin/admins/export");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        // The request is JSON (the selected ids + grid query); the response
        // is the XLSX bytes with a Content-Disposition for the browser save.
        Summary(summary => summary.Summary =
            "Export users to an XLSX workbook. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminExportUsersRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var bytes = await adminAccountService.ExportUsersAsync(actorId, req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"simf-users-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
