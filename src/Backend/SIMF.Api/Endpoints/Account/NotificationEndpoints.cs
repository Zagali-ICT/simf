// Tests: SIMF.Api.Tests/NotificationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Notifications;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>POST /api/v1/app/account/notifications/list</c> — one page of the
/// signed-in user's notifications, newest first (P12 — D-053). The
/// optional <c>unreadOnly=true</c> filter is what the bell dropdown
/// uses.
/// </summary>
public sealed class ListNotificationsEndpoint(INotificationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<NotificationDto>>>
{
    public override void Configure()
    {
        Post("/app/account/notifications/list");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Return one page of the signed-in user's notifications.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var page = await service.ListMineAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<GridPage<NotificationDto>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/app/account/notifications/unread-count</c> — polled every
/// 60 s by the notification bell. Requires an approved account.
/// </summary>
public sealed class UnreadNotificationCountEndpoint(INotificationService service)
    : EndpointWithoutRequest<ApiResult<UnreadCountResponse>>
{
    public override void Configure()
    {
        Get("/app/account/notifications/unread-count");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary =
            "Return the count of unread notifications for the signed-in user.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var count = await service.UnreadCountMineAsync(actorId, ct);
        await Send.OkAsync(
            ApiResult<UnreadCountResponse>.Ok(new UnreadCountResponse(count)), ct);
    }
}

/// <summary><c>POST /api/v1/app/account/notifications/{id}/read</c> — mark one
/// notification as read. Idempotent.</summary>
public sealed class MarkNotificationReadRequest
{
    public Guid Id { get; set; }
}

public sealed class MarkNotificationReadEndpoint(INotificationService service)
    : Endpoint<MarkNotificationReadRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/app/account/notifications/{id:guid}/read");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Mark one notification as read (idempotent).");
    }

    public override async Task HandleAsync(
        MarkNotificationReadRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.MarkReadMineAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>POST /api/v1/app/account/notifications/read-all</c> — mark
/// every unread notification as read.</summary>
public sealed class MarkAllNotificationsReadEndpoint(INotificationService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/app/account/notifications/read-all");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Mark every unread notification as read.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.MarkAllReadMineAsync(actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>DELETE /api/v1/app/account/notifications/{id}</c> — remove
/// one notification. Idempotent.</summary>
public sealed class DeleteNotificationRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteNotificationEndpoint(INotificationService service)
    : Endpoint<DeleteNotificationRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/account/notifications/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Remove one notification (idempotent).");
    }

    public override async Task HandleAsync(
        DeleteNotificationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeleteMineAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
