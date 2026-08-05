// Tests: SIMF.Api.Tests/AdminGridVisitorsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/visitors/bulk-delete</c> — D-113. Type-scoped variant
/// of <see cref="BulkDeleteUsersEndpoint"/>. Soft-deletes Visitors only;
/// admin / self / wrong-type ids are silently skipped per target (the SOC
/// gets one audit row per skip).
/// </summary>
public sealed class BulkDeleteVisitorsEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminBulkDeleteRequest, ApiResult<AdminBulkDeleteResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/bulk-delete");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Soft-delete one or many visitor accounts. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminBulkDeleteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var response = await adminAccountService.BulkDeleteUsersByKindAsync(
            // D-186: Visitor bulk = audience-scope (ProfileType.IsVisitor=true or none).
            actorId, UserType.Visitor, requirePartnerScope: false, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkDeleteResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/duplicate</c> — D-113. Type-scoped variant
/// of <see cref="DuplicateUserEndpoint"/>. Refuses any source whose
/// <see cref="UserType"/> is not Visitor.
/// </summary>
public sealed class DuplicateVisitorEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminDuplicateUserRequest, ApiResult<AdminCreateUserResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/duplicate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Duplicate an existing visitor account. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminDuplicateUserRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var created = await adminAccountService.DuplicateUserByKindAsync(
            // D-186: Visitor bulk = audience-scope (ProfileType.IsVisitor=true or none).
            actorId, UserType.Visitor, requirePartnerScope: false, req, ct);
        await Send.OkAsync(ApiResult<AdminCreateUserResponse>.Ok(created), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/export</c> — D-113. Type-scoped variant of
/// <see cref="ExportUsersEndpoint"/>. Exports Visitors only; any smuggled
/// non-Visitor id is silently dropped from the workbook.
/// </summary>
public sealed class ExportVisitorsEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminExportUsersRequest>
{
    public override void Configure()
    {
        Post("/admin/visitors/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Export), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Export visitor accounts to an XLSX workbook. Requires Administrator role.");
    }

    public override async Task HandleAsync(AdminExportUsersRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var bytes = await adminAccountService.ExportUsersByKindAsync(
            // D-186: Visitor bulk = audience-scope (ProfileType.IsVisitor=true or none).
            actorId, UserType.Visitor, requirePartnerScope: false, req, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"simf-visitors-{SimfClock.Now:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/import</c> — D-113. Type-scoped variant of
/// <see cref="ImportUsersEndpoint"/>. Every imported row is forced to
/// <c>UserType = Visitor</c>; any Role column in the workbook is ignored
/// (the type-smuggling guard). The optional <c>ProfileTypeId</c> column
/// sets the visitor tier when present.
/// </summary>
public sealed class ImportVisitorsEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<EmptyRequest, ApiResult<AdminImportUsersResponse>>
{
    /// <summary>Maximum accepted upload size in bytes (5 MB) — same cap as Admin import.</summary>
    private const long MaxUploadBytes = 5L * 1024 * 1024;

    /// <summary>ZIP local-file-header magic — every .xlsx workbook starts with these four bytes.</summary>
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    public override void Configure()
    {
        Post("/admin/visitors/import");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Import), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Import visitors from an XLSX workbook. Requires Administrator role.");
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
            // D-186: Visitor import = audience-scope flag.
            actorId, partnerScope: false, bytes, ct);
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

/// <summary>
/// <c>POST /api/v1/admin/visitors/bulk-generate</c> — D-473 (#10). Bulk-generates
/// placeholder badges by profile type + count (e.g. 10 VIP + 500 Normal), each an
/// Approved visitor with default data and a minted QR. The request's
/// <c>IsDelegate</c> flag marks the batch as delegation (وفد) members, so the same
/// endpoint serves both the visitor and the delegate desks.
/// </summary>
public sealed class BulkGenerateVisitorBadgesEndpoint(IAdminUserBulkService adminAccountService)
    : Endpoint<AdminBulkGenerateBadgesRequest, ApiResult<AdminBulkGenerateBadgesResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/bulk-generate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.BulkGenerate), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Bulk-generate placeholder visitor / delegate badges by profile type + count.");
    }

    public override async Task HandleAsync(AdminBulkGenerateBadgesRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var response = await adminAccountService.BulkGenerateBadgesAsync(
            actorId, UserType.Visitor, req, ct);
        await Send.OkAsync(ApiResult<AdminBulkGenerateBadgesResponse>.Ok(response), ct);
    }
}
