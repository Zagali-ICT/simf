using FastEndpoints;
using SIMF.Application.Venue.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Public;

/// <summary>The public 2D venue map the app renders.
/// Public content (AllowAnonymous), consistent with the public booth / speaker
/// reads. Returns the active nodes with their positions + Hall/Booth links.</summary>
public sealed class PublicVenueMapEndpoint(IVenueMapService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<PublicVenueMapNode>>>
{
    public override void Configure()
    {
        Get("/app/venue-map");
        AllowAnonymous();
        Tags("Public");
        Options(b => b.CacheOutput("PublicRead")); // A6d — 45s output cache (no-op under Testing)
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<PublicVenueMapNode>>.Ok(
            await service.ListPublicAsync(ct)), ct);
}
