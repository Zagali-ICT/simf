// Tests: SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download/delete),
//        SIMF.Api.Tests/Files/FileAuthorizationTests.cs (per-service authz + IDOR).
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Files;
using SIMF.Common.Options;
using SIMF.Contracts.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Files;

/// <summary>Shared helpers for the centralized file endpoints.</summary>
internal static class FileEndpointSupport
{
    /// <summary>Builds the caller identity from the request principal so the
    /// service can authorize off the file's service policy.</summary>
    public static FileAccessContext ContextFrom(ClaimsPrincipal user)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        Guid? userId = user.ActorIdOrNull();
        var permissions = user.FindAll(PermissionCatalog.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
        return new FileAccessContext(isAuthenticated, userId, permissions);
    }

    /// <summary>True when the caller holds the permission code or the admin
    /// wildcard — the same test <c>AssetAuth.Has</c> applies to the per-category
    /// asset gates.</summary>
    public static bool Has(ClaimsPrincipal user, string code) =>
        user.HasClaim(PermissionCatalog.ClaimType, PermissionCatalog.Wildcard)
        || user.HasClaim(PermissionCatalog.ClaimType, code);

    /// <summary>Authorizes an upload (bytes or external link) for a service.
    /// The flat <c>Files.Upload</c> policy on the route only opens the endpoint; the
    /// service decides which module's write code the caller must actually hold, so
    /// holding the sponsor-logo gate is not the same as holding the
    /// identity-document gate.
    ///
    /// <para>A service that names a dedicated route is refused here outright,
    /// before any permission is consulted: its owner is a person, and this endpoint
    /// takes the owner id from a client form field, so accepting it would let a
    /// caller plant a file on anyone's profile.</para></summary>
    public static void AuthorizeUpload(ClaimsPrincipal user, FileService service)
    {
        var policy = PolicyFor(service);
        if (policy.DedicatedUploadRoute is { Length: > 0 } route)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                $"{service} files cannot be written through the generic file endpoint, which would take "
                + $"the owner from the request and let a caller write to another person's profile. Use: {route}.",
                "لا يمكن رفع هذا النوع من الملفات عبر الواجهة العامة للملفات؛ استخدم المسار المخصص له.");
        }
        RequirePermission(user, policy.UploadPermission);
    }

    /// <summary>Authorizes a delete for the service the STORED FILE carries — the
    /// route carries only a GUID, so the caller never names the service. Same
    /// reasoning as the upload gate: one flat <c>Files.Delete</c> across every
    /// service would let whoever may delete a sponsor logo delete an attendee's
    /// identity document.</summary>
    public static void AuthorizeDelete(ClaimsPrincipal user, FileService service) =>
        RequirePermission(user, PolicyFor(service).DeletePermission);

    private static FileServicePolicy PolicyFor(FileService service)
    {
        // An undefined enum value would otherwise reach Resolve's default-deny
        // throw and surface as a 500; it is a bad request, and it is the client's.
        if (!Enum.IsDefined(service))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Unknown file service.", "نوع الملف غير معروف.");
        }
        return FileServicePolicies.Resolve(service);
    }

    private static void RequirePermission(ClaimsPrincipal user, string code)
    {
        if (Has(user, code)) { return; }
        throw new ApiException(ErrorCodes.Forbidden, 403,
            $"This action requires the '{code}' permission.",
            "ليس لديك الصلاحية اللازمة لتنفيذ هذا الإجراء.");
    }

    /// <summary>Strips characters that could break / inject into the
    /// Content-Disposition header.</summary>
    public static string SanitizeForHeader(string name) =>
        new(name.Where(c => c is not ('"' or '\r' or '\n') && !char.IsControl(c)).ToArray());

    /// <summary>An RFC 6266 / RFC 5987 attachment
    /// Content-Disposition that carries a non-ASCII (e.g. Arabic) file name
    /// safely: an ASCII-only <c>filename="…"</c> fallback for legacy clients plus
    /// <c>filename*=UTF-8''&lt;pct-encoded&gt;</c> for modern ones (browsers prefer
    /// the starred form). Both parts are header-sanitized first.</summary>
    public static string AttachmentDisposition(string fileName)
    {
        var safe = SanitizeForHeader(fileName);
        var ascii = new string(safe.Where(char.IsAscii).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(ascii)) { ascii = "download"; }
        var encoded = Uri.EscapeDataString(safe);
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }
}

/// <summary>The single upload endpoint. Multipart; the service category +
/// owner ride the form; the file is validated, scanned (fail-closed in
/// Production), encrypted-per-policy and stored. <c>Files.Upload</c> opens the
/// endpoint; the service's own <c>UploadPermission</c> decides whether this caller
/// may write THAT category, and a service with a dedicated route is refused here
/// entirely (<see cref="FileEndpointSupport.AuthorizeUpload"/>).</summary>
public sealed class FileUploadRequest
{
    public FileService Service { get; set; }

    /// <summary>The owning entity's id (e.g. the speaker /
    /// booth an admin is uploading a photo for). The owner *family*
    /// (<c>OwnerEntityType</c>) is NOT accepted from the client: it is forced from
    /// the service's policy in <c>StoredFileService</c>, so a caller cannot
    /// over-post a mismatched owner family.
    ///
    /// <para>This id is only ever a CONTENT entity (a speaker, a booth, a news
    /// article). The services whose owner is a person — avatar, ID document, VIP
    /// photo — are refused on this endpoint precisely because the id arrives from
    /// the client here: their dedicated routes derive it from the authenticated
    /// subject instead.</para></summary>
    public Guid? OwnerEntityId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class FileUploadEndpoint(
    IFileService service,
    IHostEnvironment environment,
    IOptions<UploadScanningOptions> scanOptions)
    : Endpoint<FileUploadRequest, ApiResult<UploadedFileResponse>>
{
    public override void Configure()
    {
        Post("/files");
        AllowFileUploads();
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Files.Upload),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Files");
    }

    public override async Task HandleAsync(FileUploadRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        FileEndpointSupport.AuthorizeUpload(User, req.Service);

        var file = req.File;
        if (file is null || file.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "No file was uploaded.", "لم يتم رفع أي ملف.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        // Fail-closed in Production; dev/test (where scanning may be disabled) pass.
        var failClosed = environment.IsProduction() && scanOptions.Value.FailClosed;

        var command = new UploadFileCommand(
            req.Service, req.OwnerEntityId, ms.ToArray(),
            file.FileName, file.ContentType ?? string.Empty, actorId, failClosed);

        var result = await service.UploadAsync(command, ct);
        var response = new UploadedFileResponse(
            result.Id, result.Url, result.Service, result.FileType, result.IsEncrypted, result.SizeBytes);
        await Send.OkAsync(ApiResult<UploadedFileResponse>.Ok(response), ct);
    }
}

/// <summary>Record an external image link (a logo / cover hosted
/// elsewhere). Owner-upsert; the download endpoint 302-redirects to it. Gated
/// exactly like the byte upload: <c>Files.Upload</c> plus the service's own
/// <c>UploadPermission</c>, and the personal services are refused here too — a
/// link row replaces the owner's active file, so it writes the same slot.</summary>
public sealed class FileLinkRequest
{
    public FileService Service { get; set; }
    public Guid? OwnerEntityId { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed class FileLinkEndpoint(IFileService service)
    : Endpoint<FileLinkRequest, ApiResult<UploadedFileResponse>>
{
    public override void Configure()
    {
        Post("/files/link");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Files.Upload),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Files");
    }

    public override async Task HandleAsync(FileLinkRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        FileEndpointSupport.AuthorizeUpload(User, req.Service);

        var result = await service.CreateExternalLinkAsync(
            new CreateExternalLinkCommand(req.Service, req.OwnerEntityId, req.Url, actorId), ct);
        var response = new UploadedFileResponse(
            result.Id, result.Url, result.Service, result.FileType, result.IsEncrypted, result.SizeBytes);
        await Send.OkAsync(ApiResult<UploadedFileResponse>.Ok(response), ct);
    }
}

/// <summary>The single download-by-GUID endpoint. Anonymous at the route
/// (public files must serve without a token); authorization is resolved IN CODE
/// from the file's own <see cref="FileService"/> policy, so a guessed GUID for a
/// private file is rejected with a uniform 404.</summary>
public sealed class FileDownloadRoute
{
    public Guid Id { get; set; }
}

public sealed class FileDownloadEndpoint(IFileService service)
    : Endpoint<FileDownloadRoute>
{
    public override void Configure()
    {
        Get("/files/{id:guid}");
        AllowAnonymous();
        Tags("Files");
    }

    public override async Task HandleAsync(FileDownloadRoute req, CancellationToken ct)
    {
        var caller = FileEndpointSupport.ContextFrom(User);
        var download = await service.DownloadAsync(req.Id, caller, ct);

        if (download.IsRedirect && !string.IsNullOrWhiteSpace(download.RedirectUrl))
        {
            // External-link file — 302 to the (validated public https) target. The
            // URL is off-host, so remote redirects must be explicitly allowed;
            // setting Response.StatusCode alone lets FastEndpoints default to 204.
            await Send.RedirectAsync(download.RedirectUrl, isPermanent: false, allowRemoteRedirects: true);
            return;
        }
        if (download.Content is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = HttpContext.Response;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.ContentType = download.ContentType;

        // Inline only for public images; everything else (and every private file)
        // is served as an attachment to defeat stored-XSS via a served file.
        var inline = download.Tier == FileSensitivityTier.Public
            && download.FileType == FileType.Image;
        if (!inline)
        {
            response.Headers.ContentDisposition =
                FileEndpointSupport.AttachmentDisposition(download.FileName ?? "download");
        }
        response.Headers.CacheControl = download.Tier switch
        {
            FileSensitivityTier.Public => "public, max-age=300",
            FileSensitivityTier.Secret => "no-store",
            _ => "private, max-age=60",
        };

        await response.Body.WriteAsync(download.Content, ct);
    }
}

/// <summary>Soft-delete a file. <c>Files.Delete</c> opens the endpoint; the
/// permission that actually decides is the stored file's own
/// <c>DeletePermission</c>, read from its service — one flat delete code across
/// every service would mean whoever may delete a sponsor logo may delete an
/// attendee's identity document. 409 when the file is under a retention
/// hold.</summary>
public sealed class FileDeleteRoute
{
    public Guid Id { get; set; }
}

public sealed class FileDeleteEndpoint(IFileService service, SimfAppDbContext appDb)
    : Endpoint<FileDeleteRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/files/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Files.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Files");
    }

    public override async Task HandleAsync(FileDeleteRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();

        // The row's own service, not the caller's word for it — the route carries
        // only a GUID. An unknown id falls through unauthorized to DeleteAsync,
        // which owns the 404, so this lookup never becomes an existence oracle of
        // its own. Soft-deleted rows are included: DeleteAsync is idempotent on
        // them and their service still decides who may act on them.
        var stored = await appDb.StoredFiles.AsNoTracking()
            .Where(f => f.Id == req.Id)
            .Select(f => (FileService?)f.Service)
            .FirstOrDefaultAsync(ct);
        if (stored is { } fileService)
        {
            FileEndpointSupport.AuthorizeDelete(User, fileService);
        }

        await service.DeleteAsync(req.Id, actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>PDPL right-to-erasure: securely destroy a file's bytes
/// even under a retention hold. Gated by the privileged <c>Files.ForceDelete</c>,
/// held separately from ordinary delete so the elevated action is independently
/// grantable and audited.</summary>
public sealed class FileForceDeleteEndpoint(IFileService service)
    : Endpoint<FileDeleteRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/files/{id:guid}/force");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Files.ForceDelete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Files");
    }

    public override async Task HandleAsync(FileDeleteRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.ForceDeleteAsync(req.Id, actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
