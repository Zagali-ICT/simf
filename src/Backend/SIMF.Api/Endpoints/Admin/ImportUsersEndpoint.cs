// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/users/import</c> — bulk-creates users from an XLSX
/// workbook upload (decision D-044 b). Multipart form upload, single file
/// field "file". Returns the per-row outcome summary.
/// </summary>
public sealed class ImportUsersEndpoint(IAdminAccountService adminAccountService)
    : Endpoint<EmptyRequest, ApiResult<AdminImportUsersResponse>>
{
    public override void Configure()
    {
        Post("/admin/users/import");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly));
        Tags("Admin");
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Import users from an XLSX workbook. Requires Administrator role.");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var file = Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new DataValidationException(
                "An Excel file is required.",
                "ملف Excel مطلوب.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var response = await adminAccountService.ImportUsersAsync(
            actorId, stream.ToArray(), ct);
        await Send.OkAsync(ApiResult<AdminImportUsersResponse>.Ok(response), ct);
    }
}
