// Tests: SIMF.Api.Tests/InterestTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/interests</c> — every active interest, ordered
/// for the visitor picker (P9 — D-050; الاهتمامات). Auth required so the
/// data sits behind the same access-token gate as the rest of the
/// user-profile surface, even though the rows are not sensitive.
/// </summary>
public sealed class InterestsListEndpoint(IInterestService service)
    : EndpointWithoutRequest<ApiResult<InterestListResponse>>
{
    public override void Configure()
    {
        Get("/app/account/interests");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Return active interests for the visitor profile picker.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var interests = await service.ListActiveAsync(ct);
        await Send.OkAsync(
            ApiResult<InterestListResponse>.Ok(new InterestListResponse(interests)),
            ct);
    }
}
