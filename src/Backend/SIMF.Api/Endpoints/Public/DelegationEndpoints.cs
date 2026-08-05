// Tests: SIMF.Api.Tests/DelegationsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Delegations;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-499 (Figma 1426:10771 الوفود) — <c>GET /api/v1/app/delegations</c>:
/// the invited countries' delegations (flag, name, head of delegation, date range,
/// member count) plus the two aggregate stats (participating countries + total
/// participants). Public / anonymous — only the designated head (a public figure) +
/// a member count are exposed, no member PII — consistent with the speakers /
/// booths / sponsors directories.
///
/// <para>G2 (D-811) — the list is per-viewer: a signed-in caller does not see their
/// OWN delegation (the country matching their profile nationality), and the two
/// aggregate stats are recomputed over what is shown. The endpoint stays anonymous —
/// with no bearer token there is no <c>sub</c> claim, so a guest gets the full
/// list. No output cache is configured here, so per-caller filtering poisons
/// nothing.</para></summary>
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

    public override async Task HandleAsync(CancellationToken ct)
    {
        // The bearer token is optional here: authentication still runs on an
        // anonymous endpoint, so a signed-in caller carries `sub` and an anonymous
        // one does not (same pattern as SubmitContactInquiryEndpoint).
        Guid? viewerUserId = User.ActorIdOrNull();
        var delegations = await service.GetAsync(viewerUserId, ct);
        await Send.OkAsync(ApiResult<AppDelegations>.Ok(delegations), ct);
    }
}
