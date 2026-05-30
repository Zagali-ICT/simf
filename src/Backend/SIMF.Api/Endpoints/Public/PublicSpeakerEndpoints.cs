// Tests: SIMF.Api.Tests/PublicSpeakersTests.cs
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-199 (Mockup page 19 "Speakers") — public anonymous list of
/// active speakers, ordered by DisplayOrder. Mirrors
/// ListPublicBoothsEndpoint / ListPublicDelegationsEndpoint's shape.</summary>
public sealed class ListPublicSpeakersEndpoint(IPublicSpeakerService service)
    : EndpointWithoutRequest<ApiResult<PublicSpeakers>>
{
    public override void Configure()
    {
        Get("/speakers");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicSpeakers>.Ok(
            await service.ListAsync(ct)), ct);
}

public sealed class GetPublicSpeakerRoute { public Guid Id { get; set; } }

/// <summary>D-199 (Mockup page 20 "Speaker profile") — public anonymous
/// single active speaker by id, including the speaker's sessions.</summary>
public sealed class GetPublicSpeakerEndpoint(IPublicSpeakerService service)
    : Endpoint<GetPublicSpeakerRoute, ApiResult<PublicSpeakerDetail>>
{
    public override void Configure()
    {
        Get("/speakers/{id:guid}");
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
