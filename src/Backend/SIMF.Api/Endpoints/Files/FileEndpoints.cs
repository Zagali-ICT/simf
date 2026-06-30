// Tests: SIMF.Api.Tests/Files/FilesEndpointsTests.cs (upload/download/delete),
//        SIMF.Api.Tests/Files/FileAuthorizationTests.cs (per-service authz + IDOR).
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIMF.Api.Endpoints.Admin;
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
        Guid? userId = Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
        var permissions = user.FindAll(PermissionCatalog.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
        return new FileAccessContext(isAuthenticated, userId, permissions);
    }

    /// <summary>Strips characters that could break / inject into the
    /// Content-Disposition header.</summary>
    public static string SanitizeForHeader(string name) =>
        new(name.Where(c => c is not ('"' or '\r' or '\n') && !char.IsControl(c)).ToArray());
}

/// <summary>D-568 — the single upload endpoint. Multipart; the service category +
/// owner ride the form; the file is validated, scanned (fail-closed in
/// Production), encrypted-per-policy and stored. Gated by <c>Files.Upload</c>.</summary>
public sealed class FileUploadRequest
{
    public FileService Service { get; set; }
    public FileOwnerEntityType OwnerEntityType { get; set; }
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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

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
            req.Service, req.OwnerEntityType, req.OwnerEntityId, ms.ToArray(),
            file.FileName, file.ContentType ?? string.Empty, actorId, failClosed);

        var result = await service.UploadAsync(command, ct);
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
            HttpContext.Response.StatusCode = StatusCodes.Status302Found;
            HttpContext.Response.Headers.Location = download.RedirectUrl;
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
            var name = FileEndpointSupport.SanitizeForHeader(download.FileName ?? "download");
            response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";
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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeleteAsync(req.Id, actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
