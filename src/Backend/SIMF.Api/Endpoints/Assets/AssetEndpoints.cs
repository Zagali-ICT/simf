// Tests: SIMF.Api.Tests/AssetEndpointsTests.cs
using System.Security.Claims;
using System.Security.Cryptography;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Assets.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;

namespace SIMF.Api.Endpoints.Assets;

/// <summary>D-357 — shared helpers for the generic media-asset endpoints.
/// Authorization is resolved per category through
/// <see cref="AssetPermissionRegistry"/> and enforced imperatively (the route
/// carries the category), so one endpoint family gates every entity by its own
/// existing permission. A signed-in user holding the category's code — or the
/// Administrator wildcard — is authorised.</summary>
internal static class AssetAuth
{
    public static bool Has(ClaimsPrincipal user, string code) =>
        user.HasClaim(PermissionCatalog.ClaimType, PermissionCatalog.Wildcard)
        || user.HasClaim(PermissionCatalog.ClaimType, code);

    public static bool TryParseCategory(string raw, out AssetCategory category) =>
        Enum.TryParse(raw, ignoreCase: true, out category) && Enum.IsDefined(category);

    /// <summary>Resolve + write the active asset to the response: 404 when absent,
    /// a 302 to the external URL for a link, a 304 on a matching If-None-Match, or
    /// the streamed bytes for an upload.
    /// <para>Every bodyless branch (404 / 302 / 304) must <c>StartAsync</c> the
    /// response before returning: this is a no-DTO <c>Endpoint&lt;TRequest&gt;</c>,
    /// and FastEndpoints auto-sends <c>204 No Content</c> when such a handler
    /// returns before the response has started — which previously turned the
    /// untested 404 / 302 paths into 204s. The upload branch is unaffected because
    /// writing the body starts the response.</para></summary>
    public static async Task ServeAsync(
        IAssetService service, AssetCategory category, Guid ownerId,
        bool requireOwnerActive, string cacheControl, HttpContext http, CancellationToken ct)
    {
        var resolution = await service.ResolveAsync(category, ownerId, requireOwnerActive, ct);
        if (resolution is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            await http.Response.StartAsync(ct);
            return;
        }
        if (resolution.SourceType == AssetSourceType.ExternalLink
            && !string.IsNullOrWhiteSpace(resolution.ExternalUrl))
        {
            http.Response.StatusCode = StatusCodes.Status302Found;
            http.Response.Headers.Location = resolution.ExternalUrl;
            await http.Response.StartAsync(ct);
            return;
        }
        if (resolution.Content is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            await http.Response.StartAsync(ct);
            return;
        }

        // A6 — strong ETag seeded from a content hash of the bytes already in
        // memory. The Asset row has no RowVersion / hash column and the schema is
        // frozen (D-110), so a content hash is the zero-schema validator. A repeat
        // fetch that sends a matching If-None-Match gets a 304 with no body; a
        // normal fetch is unchanged (200 + bytes). The ETag is emitted on BOTH the
        // 200 and the 304 so a cache refreshing its validator from the 304 keeps it.
        var etag =
            $"\"{Convert.ToHexString(SHA256.HashData(resolution.Content)).ToLowerInvariant()}\"";
        http.Response.Headers.CacheControl = cacheControl;
        http.Response.Headers.ETag = etag;
        if (RequestMatchesETag(http.Request, etag))
        {
            http.Response.StatusCode = StatusCodes.Status304NotModified;
            await http.Response.StartAsync(ct); // flush before FastEndpoints auto-204s
            return;
        }
        http.Response.ContentType = resolution.ContentType ?? "application/octet-stream";
        await http.Response.Body.WriteAsync(resolution.Content, ct);
    }

    /// <summary>True when the request's <c>If-None-Match</c> matches the given
    /// ETag — honouring the <c>*</c> wildcard and a comma-separated tag list.
    /// Strong comparison; the serve path only ever emits strong validators.</summary>
    private static bool RequestMatchesETag(HttpRequest request, string etag)
    {
        var header = request.Headers.IfNoneMatch;
        if (header.Count == 0)
        {
            return false;
        }
        foreach (var value in header)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }
            foreach (var token in value.Split(','))
            {
                var trimmed = token.Trim();
                if (trimmed == "*" || trimmed == etag)
                {
                    return true;
                }
            }
        }
        return false;
    }
}

/// <summary>D-357 — upload (or replace) a media asset's file. Multipart; the route
/// carries the category + owner id; the kind + file ride the form. Gated by the
/// category's write permission.</summary>
public sealed class AssetUploadRequest
{
    public string Category { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public AssetKind Kind { get; set; } = AssetKind.Image;
    public IFormFile? File { get; set; }
}

public sealed class UploadAssetEndpoint(IAssetService service)
    : Endpoint<AssetUploadRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/assets/{category}/{ownerId:guid}/image");
        AllowFileUploads();
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Assets");
    }

    public override async Task HandleAsync(AssetUploadRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        if (!AssetAuth.TryParseCategory(req.Category, out var category))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Unknown asset category.", "فئة الوسائط غير معروفة.");
        }
        if (!AssetAuth.Has(User, AssetPermissionRegistry.For(category).WritePermission))
        {
            await Send.ForbiddenAsync(ct);
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
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        await service.SetUploadAsync(
            actorId, category, req.OwnerId, req.Kind, ms.ToArray(), contentType, file.FileName, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>D-357 — set (or replace) a media asset to an external link. The route
/// carries the category + owner id; the kind + URL ride the body.</summary>
public sealed class SetAssetLinkRoute : SetAssetLinkRequest
{
    public string Category { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}

public sealed class SetAssetLinkEndpoint(IAssetService service)
    : Endpoint<SetAssetLinkRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/admin/assets/{category}/{ownerId:guid}/link");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Assets");
    }

    public override async Task HandleAsync(SetAssetLinkRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        if (!AssetAuth.TryParseCategory(req.Category, out var category))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Unknown asset category.", "فئة الوسائط غير معروفة.");
        }
        if (!AssetAuth.Has(User, AssetPermissionRegistry.For(category).WritePermission))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        await service.SetExternalLinkAsync(
            actorId, category, req.OwnerId, req.Kind, req.Url ?? string.Empty, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>D-357 — route for the admin/public asset fetch endpoints.</summary>
public sealed class AssetFetchRequest
{
    public string Category { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}

/// <summary>D-357 — admin preview of an asset (CP form / view / Media Library).
/// Gated by the category's view permission; streams bytes or 302s to the link.</summary>
public sealed class AdminFetchAssetEndpoint(IAssetService service)
    : Endpoint<AssetFetchRequest>
{
    public override void Configure()
    {
        Get("/admin/assets/{category}/{ownerId:guid}/image");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Assets");
    }

    public override async Task HandleAsync(AssetFetchRequest req, CancellationToken ct)
    {
        if (!AssetAuth.TryParseCategory(req.Category, out var category))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!AssetAuth.Has(User, AssetPermissionRegistry.For(category).ViewPermission))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        // Admin preview may show a deactivated owner's asset (Media Library).
        await AssetAuth.ServeAsync(
            service, category, req.OwnerId, requireOwnerActive: false,
            "private, max-age=300", HttpContext, ct);
    }
}

/// <summary>D-357 — public, anonymous asset serve (the Website + app render this).
/// Streams the upload bytes or 302s to the external link.</summary>
public sealed class PublicFetchAssetEndpoint(IAssetService service)
    : Endpoint<AssetFetchRequest>
{
    public override void Configure()
    {
        Get("/app/assets/{category}/{ownerId:guid}/image");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(AssetFetchRequest req, CancellationToken ct)
    {
        if (!AssetAuth.TryParseCategory(req.Category, out var category))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // Public serve refuses a soft-deleted owner's image (A9).
        await AssetAuth.ServeAsync(
            service, category, req.OwnerId, requireOwnerActive: true,
            "public, max-age=300", HttpContext, ct);
    }
}

// -- D-357 — central Media Library management (gated by MediaLibrary.*) --

/// <summary>List every media asset (filter by category / kind / source / active).</summary>
public sealed class ListAssetsEndpoint(IAssetService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAssetSummary>>>
{
    public override void Configure()
    {
        Post("/admin/assets/list");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Assets");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        if (!AssetAuth.Has(User, PermissionCatalog.MediaLibrary.View))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        await Send.OkAsync(
            ApiResult<GridPage<AdminAssetSummary>>.Ok(await service.ListAsync(req, ct)), ct);
    }
}

public sealed class AssetItemRoute { public Guid Id { get; set; } }

public sealed class GetAssetEndpoint(IAssetService service)
    : Endpoint<AssetItemRoute, ApiResult<AdminAssetSummary>>
{
    public override void Configure()
    {
        Get("/admin/assets/item/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Assets");
    }

    public override async Task HandleAsync(AssetItemRoute req, CancellationToken ct)
    {
        if (!AssetAuth.Has(User, PermissionCatalog.MediaLibrary.View))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        var detail = await service.GetByIdAsync(req.Id, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminAssetSummary>.Ok(detail), ct);
    }
}

public sealed class DeactivateAssetEndpoint(IAssetService service)
    : Endpoint<AssetItemRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/assets/item/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Assets");
    }

    public override async Task HandleAsync(AssetItemRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        if (!AssetAuth.Has(User, PermissionCatalog.MediaLibrary.Manage))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class RestoreAssetRoute { public Guid Id { get; set; } }

public sealed class RestoreAssetEndpoint(IAssetService service)
    : Endpoint<RestoreAssetRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/assets/item/{id:guid}/restore");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Assets");
    }

    public override async Task HandleAsync(RestoreAssetRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        if (!AssetAuth.Has(User, PermissionCatalog.MediaLibrary.Manage))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        await service.RestoreAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
