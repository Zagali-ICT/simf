// Tests: SIMF.Api.Tests/AdminGridOthersTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/others/bulk-delete</c> — D-113. Type-scoped variant
/// of <see cref="BulkDeleteUsersEndpoint"/>. Soft-deletes Other accounts
/// only; admin / self / wrong-type ids are silently skipped per target.
/// </summary>
public sealed class BulkDeleteOthersEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminBulkDeleteRequest, ApiResult<AdminBulkDeleteResponse>>
{
    public override void Configure()
    {
        Post("/admin/others/bulk-delete");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Soft-delete one or many Other accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminBulkDeleteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var response = await adminAccountService.BulkDeleteUsersByKindAsync(
            // D-186: Other = Visitor + partner-scope (ProfileType.IsVisitor=false).
            actorId, UserType.Visitor, requirePartnerScope: true, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkDeleteResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/duplicate</c> — D-113. Type-scoped variant of
/// <see cref="DuplicateUserEndpoint"/>. Refuses any source whose
/// <see cref="UserType"/> is not Other.
/// </summary>
public sealed class DuplicateOtherEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminDuplicateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/others/duplicate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Duplicate an existing Other account. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminDuplicateUserRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var created = await adminAccountService.DuplicateUserByKindAsync(
            // D-186: Other = Visitor + partner-scope (ProfileType.IsVisitor=false).
            actorId, UserType.Visitor, requirePartnerScope: true, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(created), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/export</c> — D-113. Type-scoped variant of
/// <see cref="ExportUsersEndpoint"/>. Exports Other accounts only.
/// </summary>
public sealed class ExportOthersEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminExportUsersRequest>
{
    public override void Configure()
    {
        Post("/admin/others/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Export), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Export Other accounts to an XLSX workbook. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminExportUsersRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var bytes = await adminAccountService.ExportUsersByKindAsync(
            // D-186: Other = Visitor + partner-scope (ProfileType.IsVisitor=false).
            actorId, UserType.Visitor, requirePartnerScope: true, req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"simf-others-{SimfClock.Now:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/others/import</c> — D-113. Type-scoped variant of
/// <see cref="ImportUsersEndpoint"/>. Every imported row is forced to
/// <c>UserType = Other</c>; any Role column is ignored. Rows must carry
/// a parseable <c>ProfileTypeId</c> column — rows missing it land in the
/// error report (same constraint as the single-create Other endpoint).
/// </summary>
public sealed class ImportOthersEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<EmptyRequest, ApiResult<AdminImportUsersResponse>>
{
    private const long MaxUploadBytes = 5L * 1024 * 1024;
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    public override void Configure()
    {
        Post("/admin/others/import");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Import), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Import Other accounts from an XLSX workbook. Requires Administrator role.");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        var actorId = User.ActorId();

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

        var response = await adminAccountService.ImportUsersByKindAsync(
            // D-186: Other import = partner-scope flag.
            actorId, partnerScope: true, bytes, ct);
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
