// Tests: SIMF.Api.Tests/AppBootstrapTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Account;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/bootstrap</c> — the on-login bundle the app fetches once
/// and caches: the signed-in user (identity + app-role +
/// registration status), the unread-notification count, and the server clock.
/// One round-trip; an additive read-only aggregate composed from existing,
/// tested reads (<see cref="IAccountService.GetCurrentUserAsync"/> +
/// <see cref="INotificationService.UnreadCountMineAsync"/>). Available to any
/// signed-in account — a pending user caches its privilege + routing on login.
/// No new permission code (app self-read).
/// </summary>
public sealed class AppBootstrapEndpoint(
    IAccountService accountService,
    INotificationService notificationService,
    TimeProvider timeProvider)
    : EndpointWithoutRequest<ApiResult<AppBootstrap>>
{
    public override void Configure()
    {
        Get("/app/bootstrap");
        Tags("Account");
        Summary(summary => summary.Summary =
            "On-login bundle: current user, unread-notification count, server time.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();

        var user = await accountService.GetCurrentUserAsync(userId, ct);
        var unread = await notificationService.UnreadCountMineAsync(userId, ct);

        await Send.OkAsync(
            ApiResult<AppBootstrap>.Ok(new AppBootstrap(user, unread, timeProvider.SimfNow())),
            ct);
    }
}
