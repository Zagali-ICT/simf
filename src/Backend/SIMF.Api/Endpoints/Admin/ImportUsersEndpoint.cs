// Tests: SIMF.Api.Tests/AdminGridV2Tests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/admins/import</c> — bulk-creates users from an XLSX
/// workbook upload (decision D-044 b, hardened in D-045 H1). Multipart form
/// upload, single file field "file". Returns the per-row outcome summary.
///
/// <para>Defence-in-depth (D-045): the upload is capped at 5 MB, the first
/// four bytes must be the ZIP magic <c>50 4B 03 04</c>, and the row cap is
/// enforced at the service layer. ClosedXML is itself a Zip handler;
/// without these gates a hostile admin could submit a Zip-bomb workbook
/// and OOM the API.</para>
/// </summary>
public sealed class ImportUsersEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<EmptyRequest, ApiResult<AdminImportUsersResponse>>
{
    /// <summary>Maximum accepted upload size in bytes (5 MB).</summary>
    private const long MaxUploadBytes = 5L * 1024 * 1024;

    /// <summary>ZIP local-file-header magic — every .xlsx workbook starts with these four bytes.</summary>
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    public override void Configure()
    {
        Post("/admin/admins/import");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Import), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
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

        if (file.Length > MaxUploadBytes)
        {
            throw new ApiException(
                ErrorCodes.AdminImportEmpty, 413,
                "The Excel file is too large. The maximum is 5 MB.",
                "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();

        if (!HasZipMagic(bytes))
        {
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }

        var response = await adminAccountService.ImportUsersAsync(actorId, bytes, ct);
        await Send.OkAsync(ApiResult<AdminImportUsersResponse>.Ok(response), ct);
    }

    private static bool HasZipMagic(byte[] bytes)
    {
        if (bytes.Length < ZipMagic.Length) return false;
        for (var i = 0; i < ZipMagic.Length; i++)
        {
            if (bytes[i] != ZipMagic[i]) return false;
        }
        return true;
    }
}
