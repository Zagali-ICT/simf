// Tests: SIMF.Api.Tests/PublicMediaTests.cs
using FastEndpoints;
using SIMF.Application.Media.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-199 (Mockup page 30 — معرض الصور والفيديوهات) — public anonymous
/// paged list of active media items, optionally narrowed to one album and/or a
/// single <see cref="MediaKind"/> (image / video).
/// Anonymous to match the other public reads (ListPublicDelegations).</summary>
public sealed class ListPublicMediaRequest
{
    public string? Album { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; } = 25;

    /// <summary>A8 — optional kind filter (<c>?kind=Image</c> / <c>?kind=Video</c>);
    /// null = both kinds. Appended (append-only, D-219).</summary>
    public MediaKind? Kind { get; set; }
}

public sealed class ListPublicMediaEndpoint(IPublicMediaService service)
    : Endpoint<ListPublicMediaRequest, ApiResult<PublicMediaPage>>
{
    public override void Configure()
    {
        Get("/app/media");
        AllowAnonymous();
        Tags("Public");
        // A6d — 45s output cache; varies by all query keys so ?album/?skip/?top
        // variants keep distinct entries (no-op under Testing). Only the JSON list
        // is cached here — the image/thumbnail byte endpoints keep their own
        // client Cache-Control and are not output-cached.
        Options(b => b.CacheOutput("PublicRead"));
    }

    public override async Task HandleAsync(ListPublicMediaRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicMediaPage>.Ok(
            await service.ListAsync(req.Album, req.Skip, req.Top, req.Kind, ct)), ct);
}

public sealed class PublicMediaImageRoute { public Guid Id { get; set; } }

/// <summary>Stream an active item's primary image bytes (out-of-row,
/// D-90). 404 when the item is missing / inactive / has no stored image.
/// Anonymous: gallery images are public (matches the anonymous list).</summary>
public sealed class PublicMediaImageEndpoint(IPublicMediaService service)
    : Endpoint<PublicMediaImageRoute>
{
    public override void Configure()
    {
        Get("/app/media/{id:guid}/image");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(PublicMediaImageRoute req, CancellationToken ct)
    {
        var result = await service.GetImageAsync(req.Id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var (content, contentType) = result.Value;
        HttpContext.Response.Headers.CacheControl = "public, max-age=300";
        Stream stream = new MemoryStream(content);
        await Send.StreamAsync(stream, contentType: contentType, cancellation: ct);
    }
}

public sealed class PublicMediaThumbnailRoute { public Guid Id { get; set; } }

/// <summary>Stream an active item's thumbnail / poster bytes
/// (out-of-row). 404 when missing / inactive / no thumbnail.</summary>
public sealed class PublicMediaThumbnailEndpoint(IPublicMediaService service)
    : Endpoint<PublicMediaThumbnailRoute>
{
    public override void Configure()
    {
        Get("/app/media/{id:guid}/thumbnail");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(PublicMediaThumbnailRoute req, CancellationToken ct)
    {
        var result = await service.GetThumbnailAsync(req.Id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var (content, contentType) = result.Value;
        HttpContext.Response.Headers.CacheControl = "public, max-age=300";
        Stream stream = new MemoryStream(content);
        await Send.StreamAsync(stream, contentType: contentType, cancellation: ct);
    }
}
