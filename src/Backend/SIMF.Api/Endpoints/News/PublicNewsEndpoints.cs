// Tests: SIMF.Api.Tests/NewsTests.cs
using FastEndpoints;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Api.Endpoints.News;

/// <summary>D-199 (Mockup screen 29) — public, anonymous, paged News feed.
/// Active + already-published articles only, newest first. Anonymous to match
/// the existing public reads (<c>ListPublicDelegationsEndpoint</c>).</summary>
public sealed class ListPublicNewsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListPublicNewsEndpoint(IPublicNewsService service)
    : Endpoint<ListPublicNewsQuery, ApiResult<PublicNewsPage>>
{
    public override void Configure()
    {
        Get("/app/news");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(ListPublicNewsQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicNewsPage>.Ok(
            await service.ListAsync(req.Page, req.PageSize, ct)), ct);
}

public sealed class GetPublicNewsRoute { public Guid Id { get; set; } }

/// <summary>D-199 (Mockup screen 29b) — public, anonymous single article.</summary>
public sealed class GetPublicNewsEndpoint(IPublicNewsService service)
    : Endpoint<GetPublicNewsRoute, ApiResult<PublicNewsArticle>>
{
    public override void Configure()
    {
        Get("/app/news/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(GetPublicNewsRoute req, CancellationToken ct)
    {
        var article = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.NotFound, 404,
                "The news article was not found.",
                "لم يتم العثور على الخبر.");
        await Send.OkAsync(ApiResult<PublicNewsArticle>.Ok(article), ct);
    }
}
