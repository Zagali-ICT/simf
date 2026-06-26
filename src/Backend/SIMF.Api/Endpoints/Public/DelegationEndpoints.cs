// Tests: SIMF.Api.Tests/DelegationsTests.cs
using FastEndpoints;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Delegations;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-499 (Figma 1426:10771 الوفود) — <c>GET /api/v1/app/delegations</c>:
/// the invited countries' delegations (flag, name, head of delegation, date range,
/// member count) plus the two aggregate stats (participating countries + total
/// participants). Public / anonymous — only the designated head (a public figure) +
/// a member count are exposed, no member PII — consistent with the speakers /
/// booths / sponsors directories.</summary>
public sealed class ListPublicDelegationsEndpoint(IPublicDelegationService service)
    : EndpointWithoutRequest<ApiResult<AppDelegations>>
{
    public override void Configure()
    {
        Get("/app/delegations");
        AllowAnonymous();
        Tags("App");
        Summary(summary => summary.Summary =
            "Delegations: invited countries with head of delegation, dates and member count.");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AppDelegations>.Ok(await service.GetAsync(ct)), ct);
}
