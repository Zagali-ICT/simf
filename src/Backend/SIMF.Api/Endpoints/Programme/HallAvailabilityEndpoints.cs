// Tests: SIMF.Api.Tests/HallAvailabilityTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>The team defines hall availability
/// windows (the "hall time" for meetings); the admin meeting-review flow
/// binds an accepted request to a free hall slot.
/// <para>Gated by the hall-scoped <c>HallAvailability.Manage</c> /
/// <c>.View</c> pair, NOT by <c>SpeakerMeetingRequests.*</c>: the windows are a
/// property of the hall and their free slots are read by BOTH meeting desks
/// (speaker <i>and</i> delegation), so borrowing the speaker desk's code locked a
/// delegation-only or halls-only operator out of a surface they legitimately
/// run.</para></summary>
public sealed class CreateHallAvailabilityWindowEndpoint(IHallAvailabilityService service)
    : Endpoint<CreateHallAvailabilityWindowRequest, ApiResult<AdminHallAvailabilityWindow>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAvailability.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        CreateHallAvailabilityWindowRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAvailability.View),
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
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAvailability.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeleteWindowAsync(actorId, Route<Guid>("id"), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>The free meeting slots for a hall (the admin review flow reads
/// these before binding an accepted request to a hall slot). Empty when the
/// hall has no future windows.
/// <para>Read by the speaker-meeting AND the delegation-meeting Approve
/// modals, so it carries the shared <c>HallAvailability.View</c> code: a
/// meeting-desk role needs that one grant instead of the unrelated
/// <c>SpeakerMeetingRequests.View</c>.</para></summary>
public sealed class GetHallAvailableSlotsEndpoint(IHallAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<HallAvailableSlot>>>
{
    public override void Configure()
    {
        Get("/admin/halls/{hallId:guid}/available-slots");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAvailability.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<HallAvailableSlot>>.Ok(
            await service.GetAvailableSlotsAsync(Route<Guid>("hallId"), ct)), ct);
}
