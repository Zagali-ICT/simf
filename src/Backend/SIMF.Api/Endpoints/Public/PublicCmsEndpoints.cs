// Tests: SIMF.Api.Tests/CmsTests.cs
using FastEndpoints;
using SIMF.Application.Cms.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Cms;

namespace SIMF.Api.Endpoints.Public;

// -- D-173 (gap doc G8, PDF §1) — public CMS read surface -----------------

public sealed class GetPublicContentBlockRoute
{
    public string Key { get; set; } = string.Empty;
}

public sealed class GetPublicContentBlockEndpoint(IPublicCmsService service)
    : Endpoint<GetPublicContentBlockRoute, ApiResult<PublicContentBlock>>
{
    public override void Configure()
    {
        Get("/content/{key}");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(GetPublicContentBlockRoute req, CancellationToken ct)
    {
        var block = await service.GetContentBlockAsync(req.Key, ct);
        if (block is null)
        {
            throw new ApiException(
                ErrorCodes.ContentBlockNotFound, 404,
                "Content block not found.",
                "لم يتم العثور على المحتوى.");
        }

        // D-173: If-Modified-Since handshake. HTTP date precision is
        // one second, so the server-side LastUpdatedAt is truncated to
        // the second before comparison and before being emitted as
        // Last-Modified — otherwise a millisecond drift makes the very
        // next request a cache miss.
        var lastModifiedSecond = block.LastUpdatedAt.AddTicks(
            -(block.LastUpdatedAt.Ticks % TimeSpan.TicksPerSecond));
        var ifModifiedSince = HttpContext.Request.Headers.IfModifiedSince;
        if (ifModifiedSince.Count > 0
            && DateTimeOffset.TryParse(ifModifiedSince.ToString(), out var since)
            && since >= lastModifiedSecond)
        {
            await Send.ResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        HttpContext.Response.Headers.LastModified =
            lastModifiedSecond.UtcDateTime.ToString("R");
        await Send.OkAsync(ApiResult<PublicContentBlock>.Ok(block), ct);
    }
}

public sealed class BatchPublicContentBlocksEndpoint(IPublicCmsService service)
    : Endpoint<PublicContentBlockBatchRequest, ApiResult<PublicContentBlockBatch>>
{
    public override void Configure()
    {
        Post("/content/batch");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(PublicContentBlockBatchRequest req, CancellationToken ct)
    {
        var result = await service.GetContentBlocksAsync(
            req.Keys.ToList(), ct);
        await Send.OkAsync(ApiResult<PublicContentBlockBatch>.Ok(result), ct);
    }
}

public sealed class GetPublicBannersEndpoint(IPublicCmsService service)
    : EndpointWithoutRequest<ApiResult<PublicBanners>>
{
    public override void Configure()
    {
        Get("/banners");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicBanners>.Ok(
            await service.GetActiveBannersAsync(ct)), ct);
}
