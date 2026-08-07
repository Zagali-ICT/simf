// Tests: SIMF.Api.Tests/RecommendationServiceTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Recommendations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Recommendations;

namespace SIMF.Api.Endpoints.Recommendations;

/// <summary>Public "Meet People Like
/// You" endpoint. Returns the caller's top N interest-matched peers.
/// Requires an approved authenticated account.</summary>
public sealed class MeetPeopleLikeYouRequest
{
    public int? Take { get; set; }
}

public sealed class MeetPeopleLikeYouEndpoint(IRecommendationService service)
    : Endpoint<MeetPeopleLikeYouRequest, ApiResult<RecommendationsResponse>>
{
    public override void Configure()
    {
        Get("/app/account/recommendations/meet-like-you");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(MeetPeopleLikeYouRequest req, CancellationToken ct)
    {
        var callerId = User.ActorId();
        var result = await service.MeetPeopleLikeYouAsync(
            callerId, req.Take ?? 20, ct);
        await Send.OkAsync(ApiResult<RecommendationsResponse>.Ok(result), ct);
    }
}
