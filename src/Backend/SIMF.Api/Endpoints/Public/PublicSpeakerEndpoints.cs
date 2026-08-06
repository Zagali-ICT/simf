// Tests: SIMF.Api.Tests/PublicSpeakersTests.cs
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Public anonymous list of
/// active speakers, ordered by DisplayOrder. Mirrors
/// ListPublicBoothsEndpoint / ListPublicDelegationsEndpoint's shape.</summary>
public sealed class ListPublicSpeakersEndpoint(IPublicSpeakerService service)
    : EndpointWithoutRequest<ApiResult<PublicSpeakers>>
{
    public override void Configure()
    {
        Get("/app/speakers");
        AllowAnonymous();
        Tags("Public");
        Options(b => b.CacheOutput("PublicRead")); // 45s output cache (no-op under Testing)
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicSpeakers>.Ok(
            await service.ListAsync(ct)), ct);
}

public sealed class GetPublicSpeakerRoute { public Guid Id { get; set; } }

/// <summary>Public anonymous
/// single active speaker by id, including the speaker's sessions.</summary>
public sealed class GetPublicSpeakerEndpoint(IPublicSpeakerService service)
    : Endpoint<GetPublicSpeakerRoute, ApiResult<PublicSpeakerDetail>>
{
    public override void Configure()
    {
        Get("/app/speakers/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(GetPublicSpeakerRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");
        await Send.OkAsync(ApiResult<PublicSpeakerDetail>.Ok(detail), ct);
    }
}
