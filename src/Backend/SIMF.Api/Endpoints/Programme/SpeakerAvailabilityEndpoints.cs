// Tests: SIMF.Api.Tests/SpeakerAvailabilityTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>The team defines speaker availability
/// windows; the VIP-meeting flow reads the free slots derived from them. Admin
/// window CRUD is gated by <c>SpeakerMeetingRequests.Manage</c>/<c>.View</c>; the
/// free-slot read needs an approved account.</summary>
public sealed class CreateSpeakerAvailabilityWindowEndpoint(ISpeakerAvailabilityService service)
    : Endpoint<CreateSpeakerAvailabilityWindowRequest, ApiResult<AdminSpeakerAvailabilityWindow>>
{
    public override void Configure()
    {
        Post("/admin/speakers/{speakerId:guid}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        CreateSpeakerAvailabilityWindowRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var speakerId = Route<Guid>("speakerId");
        await Send.OkAsync(ApiResult<AdminSpeakerAvailabilityWindow>.Ok(
            await service.CreateWindowAsync(actorId, speakerId, req, ct)), ct);
    }
}

public sealed class ListSpeakerAvailabilityWindowsEndpoint(ISpeakerAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>>
{
    public override void Configure()
    {
        Get("/admin/speakers/{speakerId:guid}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>.Ok(
            await service.ListWindowsAsync(Route<Guid>("speakerId"), ct)), ct);
}

public sealed class DeleteSpeakerAvailabilityWindowEndpoint(ISpeakerAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/speaker-availability-windows/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
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

/// <summary>The free meeting slots for a speaker (an approved attendee
/// reads these before requesting a VIP meeting). Empty when the speaker has no
/// future windows.</summary>
public sealed class GetSpeakerAvailableSlotsEndpoint(ISpeakerAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<SpeakerAvailableSlot>>>
{
    public override void Configure()
    {
        Get("/app/speakers/{speakerId:guid}/available-slots");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Programme");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<SpeakerAvailableSlot>>.Ok(
            await service.GetAvailableSlotsAsync(Route<Guid>("speakerId"), ct)), ct);
}
