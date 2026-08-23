// Tests: SIMF.Api.Tests/AccountDeletionTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>DELETE /api/v1/app/account</c> — erases the signed-in user's own account.
/// </summary>
/// <remarks>
/// <para>Google Play requires an in-app deletion path for any app that offers
/// account creation. This is it.</para>
/// <para>Deliberately NOT gated on <c>RequireApprovedAccount</c>. A pending,
/// rejected or disabled holder is exactly the person who most wants to be
/// erased, and gating on approval would leave them with no way out — the
/// precedent is <c>CurrentUserEndpoint</c>, which is open to any signed-in
/// account for the same reason. The caller can only ever erase themselves:
/// the subject is the <c>sub</c> claim, never a route parameter.</para>
/// </remarks>
public sealed class AccountDeleteEndpoint(IAccountDeletionService deletion)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/account");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary =>
        {
            summary.Summary = "Permanently erase the signed-in user's account.";
            summary.Description =
                "Removes the holder's personal data, destroys their identity "
                + "document and photos, revokes every session and device key, "
                + "and disables the account. Irreversible. Idempotent.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await deletion.DeleteOwnAccountAsync(User.ActorId(), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
