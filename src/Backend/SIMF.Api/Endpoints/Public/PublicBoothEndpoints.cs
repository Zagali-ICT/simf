// Tests: SIMF.Api.Tests/PublicBoothsTests.cs
using FastEndpoints;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Exhibition;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-199 (Mockup page 22) — public anonymous list of active
/// booths, feeding the exhibition page and the 2D venue map. Mirrors
/// ListPublicDelegationsEndpoint's shape.</summary>
public sealed class ListPublicBoothsEndpoint(IPublicBoothService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<PublicBoothSummary>>>
{
    public override void Configure()
    {
        Get("/app/booths");
        AllowAnonymous();
        Tags("Public");
        Options(b => b.CacheOutput("PublicRead")); // A6d — 45s output cache (no-op under Testing)
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<PublicBoothSummary>>.Ok(
            await service.ListAsync(ct)), ct);
}

public sealed class GetPublicBoothRoute { public Guid Id { get; set; } }

/// <summary>Public anonymous single active booth by id.</summary>
public sealed class GetPublicBoothEndpoint(IPublicBoothService service)
    : Endpoint<GetPublicBoothRoute, ApiResult<PublicBoothDetail>>
{
    public override void Configure()
    {
        Get("/app/booths/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(GetPublicBoothRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");
        await Send.OkAsync(ApiResult<PublicBoothDetail>.Ok(detail), ct);
    }
}
