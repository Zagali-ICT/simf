// Tests: SIMF.Api.Tests/AdminMediaTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using SIMF.Api.RequestContext;
using SIMF.Application.Media.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Media;

namespace SIMF.Api.Endpoints.Admin;

public sealed class ListMediaEndpoint(IAdminMediaService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminMediaSummary>>>
{
    public override void Configure()
    {
        Post("/admin/media/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminMediaSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetMediaRoute { public Guid Id { get; set; } }

public sealed class GetMediaEndpoint(IAdminMediaService service)
    : Endpoint<GetMediaRoute, ApiResult<AdminMediaDetail>>
{
    public override void Configure()
    {
        Get("/admin/media/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetMediaRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.MediaNotFound, 404,
                "The media item was not found.",
                "لم يتم العثور على عنصر الوسائط.");
        await Send.OkAsync(ApiResult<AdminMediaDetail>.Ok(detail), ct);
    }
}

public sealed class CreateMediaEndpoint(IAdminMediaService service)
    : Endpoint<AdminCreateMediaRequest, ApiResult<AdminMediaDetail>>
{
    public override void Configure()
    {
        Post("/admin/media");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateMediaRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminMediaDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateMediaRoute : AdminUpdateMediaRequest { public Guid Id { get; set; } }

public sealed class UpdateMediaEndpoint(IAdminMediaService service)
    : Endpoint<UpdateMediaRoute, ApiResult<AdminMediaDetail>>
{
    public override void Configure()
    {
        Put("/admin/media/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateMediaRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminMediaDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteMediaRoute { public Guid Id { get; set; } }

public sealed class DeleteMediaEndpoint(IAdminMediaService service)
    : Endpoint<DeleteMediaRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/media/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteMediaRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Upload (or replace) a media item's primary image.
/// Multipart form upload; bytes persisted out-of-row via
/// <c>IMediaImageStorage</c> (mirrors <c>AvatarUploadEndpoint</c>). The route
/// id is bound from the URL; the file rides the form.</summary>
public sealed class MediaImageUploadRequest
{
    public Guid Id { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class MediaImageUploadEndpoint(IAdminMediaService service)
    : Endpoint<MediaImageUploadRequest, ApiResult<AdminMediaDetail>>
{
    public override void Configure()
    {
        Post("/admin/media/{id:guid}/image");
        AllowFileUploads();
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Media.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(MediaImageUploadRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();

        var file = req.File;
        if (file is null || file.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "No file was uploaded.",
                "لم يتم رفع أي ملف.");
        }
        if (file.Length > 10 * 1024 * 1024)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "Image must be 10 MB or smaller.",
                "يجب أن تكون الصورة 10 ميجابايت أو أقل.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        await Send.OkAsync(ApiResult<AdminMediaDetail>.Ok(
            await service.SetImageAsync(actorId, req.Id, bytes, contentType, ct)), ct);
    }
}
