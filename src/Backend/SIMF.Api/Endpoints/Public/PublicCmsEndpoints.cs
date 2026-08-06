// Tests: SIMF.Api.Tests/CmsTests.cs
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
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
        Get("/app/content/{key}");
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

        // If-Modified-Since handshake. HTTP date precision is
        // one second, so the server-side LastUpdatedAt is truncated to
        // the second before comparison and before being emitted as
        // Last-Modified — otherwise a millisecond drift makes the very
        // next request a cache miss.
        var lastModifiedSecond = block.LastUpdatedAt.AddTicks(
            -(block.LastUpdatedAt.Ticks % TimeSpan.TicksPerSecond));
        var ifModifiedSince = HttpContext.Request.Headers.IfModifiedSince;
        if (ifModifiedSince.Count > 0
            && DateTime.TryParse(ifModifiedSince.ToString(), out var since)
            && since >= lastModifiedSecond)
        {
            await Send.ResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        HttpContext.Response.Headers.LastModified =
            lastModifiedSecond.ToString("R");
        await Send.OkAsync(ApiResult<PublicContentBlock>.Ok(block), ct);
    }
}

public sealed class BatchPublicContentBlocksEndpoint(IPublicCmsService service)
    : Endpoint<PublicContentBlockBatchRequest, ApiResult<PublicContentBlockBatch>>
{
    public override void Configure()
    {
        Post("/app/content/batch");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(PublicContentBlockBatchRequest req, CancellationToken ct)
    {
        // R1 audit (#28) — a {"keys": null} body overwrites the property
        // initializer with null on deserialization. The validator rejects a
        // null/empty list with a clean 400; this guard keeps the handler
        // NRE-safe as defense-in-depth if the request ever reaches it.
        var result = await service.GetContentBlocksAsync(
            req.Keys?.ToList() ?? new List<string>(), ct);
        await Send.OkAsync(ApiResult<PublicContentBlockBatch>.Ok(result), ct);
    }
}

/// <summary>R1 audit (#28) — rejects a null/empty <c>Keys</c> list on the public
/// batch read with a clean bilingual 400 (validation_failed) instead of letting
/// a <c>{"keys": null}</c> body fault the handler. Auto-discovered by
/// FastEndpoints and bound to the endpoint's request type.</summary>
public sealed class PublicContentBlockBatchRequestValidator
    : Validator<PublicContentBlockBatchRequest>
{
    public PublicContentBlockBatchRequestValidator()
    {
        RuleFor(request => request.Keys)
            .NotEmpty().Bilingual(
                "At least one content-block key is required.",
                "يجب تحديد مفتاح محتوى واحد على الأقل.");
    }
}

public sealed class GetPublicBannersEndpoint(IPublicCmsService service)
    : EndpointWithoutRequest<ApiResult<PublicBanners>>
{
    public override void Configure()
    {
        Get("/app/banners");
        AllowAnonymous();
        Tags("Public");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicBanners>.Ok(
            await service.GetActiveBannersAsync(ct)), ct);
}
