// Tests: SIMF.Api.Tests/CurrentUserEndpointTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/users/me</c> — the signed-in user the Flutter app decodes
/// into its <c>CurrentUserDto</c>: identity, the
/// resolved mobile app-role, the preferred language and the registration
/// status. Available to ANY signed-in account — including not-yet-approved
/// ones — so the app can poll the approval state on the Registration-Status
/// screen. Mirrors <see cref="ProfileEndpoint"/>'s auth model
/// (signed-in, own <c>sub</c>); no permission code (app self-read, not an admin
/// action).
/// </summary>
public sealed class CurrentUserEndpoint(IAccountService accountService)
    : EndpointWithoutRequest<ApiResult<CurrentUserResponse>>
{
    public override void Configure()
    {
        Get("/app/users/me");
        Tags("Account");
        Summary(summary => summary.Summary =
            "Get the signed-in user (identity + app-role + registration status).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();

        var response = await accountService.GetCurrentUserAsync(userId, ct);
        await Send.OkAsync(ApiResult<CurrentUserResponse>.Ok(response), ct);
    }
}
