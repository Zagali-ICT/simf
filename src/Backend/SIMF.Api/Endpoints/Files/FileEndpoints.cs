// Tests: SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download/delete),
//        SIMF.Api.Tests/Files/FileAuthorizationTests.cs (per-service authz + IDOR).
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
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

namespace SIMF.Api.Endpoints.Files;

/// <summary>D-568 — shared helpers for the centralized file endpoints.</summary>
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

    /// <summary>Strips characters that could break / inject into the
    /// Content-Disposition header.</summary>
    public static string SanitizeForHeader(string name) =>
        new(name.Where(c => c is not ('"' or '\r' or '\n') && !char.IsControl(c)).ToArray());

    /// <summary>P1 (D-568 hardening) — an RFC 6266 / RFC 5987 attachment
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

/// <summary>D-568 — the single upload endpoint. Multipart; the service category +
/// owner ride the form; the file is validated, scanned (fail-closed in
/// Production), encrypted-per-policy and stored. Gated by <c>Files.Upload</c>.</summary>
public sealed class FileUploadRequest
{
    public FileService Service { get; set; }

    /// <summary>P2 (D-568 hardening) — the owning entity's id (e.g. the speaker /
    /// booth an admin is uploading a photo for). The owner *family*
    /// (<c>OwnerEntityType</c>) is NOT accepted from the client: it is forced from
    /// the service's policy in <c>StoredFileService</c>, so a caller cannot
    /// over-post a mismatched owner family. For owner-scoped self-service uploads
    /// (avatar / ID) the id is server-derived from the subject by the internal
    /// caller, never trusted from this form.</summary>
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

/// <summary>D-568 (P6) — record an external image link (a logo / cover hosted
/// elsewhere). Owner-upsert; the download endpoint 302-redirects to it. Gated by
/// <c>Files.Upload</c>, same as the byte upload.</summary>
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

        var result = await service.CreateExternalLinkAsync(
            new CreateExternalLinkCommand(req.Service, req.OwnerEntityId, req.Url, actorId), ct);
        var response = new UploadedFileResponse(
            result.Id, result.Url, result.Service, result.FileType, result.IsEncrypted, result.SizeBytes);
        await Send.OkAsync(ApiResult<UploadedFileResponse>.Ok(response), ct);
    }
}

/// <summary>D-568 — the single download-by-GUID endpoint. Anonymous at the route
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

/// <summary>D-568 — soft-delete a file. Gated by <c>Files.Delete</c>; 409 when the
/// file is under a retention hold.</summary>
public sealed class FileDeleteRoute
{
    public Guid Id { get; set; }
}

public sealed class FileDeleteEndpoint(IFileService service)
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
        await service.DeleteAsync(req.Id, actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>D-568 — PDPL right-to-erasure (P7): securely destroy a file's bytes
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
