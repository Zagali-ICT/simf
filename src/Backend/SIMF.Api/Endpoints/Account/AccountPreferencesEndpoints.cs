// Tests: SIMF.Api.Tests/AccountPreferencesTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Preferences;
using SIMF.Common;
using SIMF.Contracts.Account;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/preferences</c> — the signed-in account's five
/// accessibility choices (`accessibility-server-sync`). Returns
/// <see cref="AccountPreferences.Defaults"/> — not a 404 — for an account that
/// has never saved any, so the app's first read on a fresh device always
/// succeeds. Approved account, own <c>sub</c>; an app-user surface, so no
/// admin permission policy.
/// </summary>
public sealed class AccountPreferencesGetEndpoint(IAccountPreferencesService service)
    : EndpointWithoutRequest<ApiResult<AccountPreferences>>
{
    public override void Configure()
    {
        Get("/app/account/preferences");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary =
            "The signed-in account's accessibility preferences (defaults when never saved).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();

        var preferences = await service.GetMineAsync(actorId, ct);
        await Send.OkAsync(ApiResult<AccountPreferences>.Ok(preferences), ct);
    }
}

/// <summary>
/// <c>PUT /api/v1/app/account/preferences</c> — stores the five accessibility
/// choices against the signed-in account and returns the persisted set. A full
/// replace, so re-sending the same body changes nothing; an unknown
/// <c>textSize</c> is a bilingual 400 <c>VALIDATION_FAILED</c> rather than a
/// silent coercion. Approved account, own <c>sub</c>; no admin permission.
/// </summary>
public sealed class AccountPreferencesUpdateEndpoint(IAccountPreferencesService service)
    : Endpoint<UpdateAccountPreferencesRequest, ApiResult<AccountPreferences>>
{
    public override void Configure()
    {
        Put("/app/account/preferences");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Store the signed-in account's accessibility preferences (idempotent full replace).");
    }

    public override async Task HandleAsync(
        UpdateAccountPreferencesRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();

        var preferences = await service.SaveMineAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<AccountPreferences>.Ok(preferences), ct);
    }
}
