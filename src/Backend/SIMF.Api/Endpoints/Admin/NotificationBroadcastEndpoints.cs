// Tests: SIMF.Api.Tests/NotificationBroadcastTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Route-bound id for the broadcast-detail endpoint.</summary>
public sealed class BroadcastByIdRequest
{
    public Guid Id { get; set; }
}

/// <summary>Control Panel "Announcements" desk — queue a manual notification
/// broadcast. The server inserts a Pending job and the background worker fans it
/// out (in-app row + email per recipient); the response carries the job id + the
/// recipient estimate at submit time.</summary>
public sealed class CreateBroadcastEndpoint(INotificationBroadcastService service)
    : Endpoint<AdminCreateBroadcastRequest, ApiResult<AdminBroadcastResult>>
{
    public override void Configure()
    {
        Post("/admin/notifications/broadcast");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Announcements.Send),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateBroadcastRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminBroadcastResult>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>Live recipient-count estimate for a broadcast target — powers the
/// composer's "Send to N recipients" as the admin picks a session / audience.</summary>
public sealed class EstimateBroadcastEndpoint(INotificationBroadcastService service)
    : Endpoint<AdminBroadcastEstimateRequest, ApiResult<AdminBroadcastEstimateResult>>
{
    public override void Configure()
    {
        Post("/admin/notifications/broadcast/estimate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Announcements.Send),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminBroadcastEstimateRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminBroadcastEstimateResult>.Ok(
            await service.EstimateAsync(req, ct)), ct);
}

/// <summary>The Announcements history grid — past broadcasts + delivery status.</summary>
public sealed class ListBroadcastsEndpoint(INotificationBroadcastService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminBroadcastSummary>>>
{
    public override void Configure()
    {
        Post("/admin/notifications/broadcasts/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Announcements.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminBroadcastSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

/// <summary>One broadcast's status detail (counters + error).</summary>
public sealed class GetBroadcastEndpoint(INotificationBroadcastService service)
    : Endpoint<BroadcastByIdRequest, ApiResult<AdminBroadcastSummary>>
{
    public override void Configure()
    {
        Get("/admin/notifications/broadcasts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Announcements.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(BroadcastByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.BroadcastNotFound, 404,
                "The broadcast was not found.",
                "لم يتم العثور على البثّ.");
        await Send.OkAsync(ApiResult<AdminBroadcastSummary>.Ok(detail), ct);
    }
}
