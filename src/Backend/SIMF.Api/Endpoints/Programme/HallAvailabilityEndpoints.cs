// Tests: SIMF.Api.Tests/HallAvailabilityTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>D-715 (item 7, FDS-013 §15 GAP-1) — the team defines hall availability
/// windows (the "hall time" for meetings); the admin meeting-review flow (GAP-2)
/// binds an accepted request to a free hall slot. Gated by the same
/// <c>SpeakerMeetingRequests.Manage</c>/<c>.View</c> permission as the sibling
/// speaker-availability admin surface — one meeting-management area.</summary>
public sealed class CreateHallAvailabilityWindowEndpoint(IHallAvailabilityService service)
    : Endpoint<CreateHallAvailabilityWindowRequest, ApiResult<AdminHallAvailabilityWindow>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        CreateHallAvailabilityWindowRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var hallId = Route<Guid>("hallId");
        await Send.OkAsync(ApiResult<AdminHallAvailabilityWindow>.Ok(
            await service.CreateWindowAsync(actorId, hallId, req, ct)), ct);
    }
}

public sealed class ListHallAvailabilityWindowsEndpoint(IHallAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AdminHallAvailabilityWindow>>>
{
    public override void Configure()
    {
        Get("/admin/halls/{hallId:guid}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminHallAvailabilityWindow>>.Ok(
            await service.ListWindowsAsync(Route<Guid>("hallId"), ct)), ct);
}

public sealed class DeleteHallAvailabilityWindowEndpoint(IHallAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/hall-availability-windows/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeleteWindowAsync(actorId, Route<Guid>("id"), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>D-715 — the free meeting slots for a hall (the admin review flow reads
/// these before binding an accepted request to a hall slot, GAP-2). Empty when the
/// hall has no future windows.</summary>
public sealed class GetHallAvailableSlotsEndpoint(IHallAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<HallAvailableSlot>>>
{
    public override void Configure()
    {
        Get("/admin/halls/{hallId:guid}/available-slots");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<HallAvailableSlot>>.Ok(
            await service.GetAvailableSlotsAsync(Route<Guid>("hallId"), ct)), ct);
}
