// Tests: SIMF.Api.Tests/DelegationAvailabilityTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>Bi-Meeting rework — the team defines delegation availability windows;
/// the delegation-meeting flow reads the free slots derived from them (parity with
/// the speaker availability endpoints). Admin window CRUD is gated by
/// <c>DelegationMeetings.Manage</c>/<c>.View</c>; the free-slot read needs an
/// approved account.</summary>
public sealed class CreateDelegationAvailabilityWindowEndpoint(IDelegationAvailabilityService service)
    : Endpoint<CreateDelegationAvailabilityWindowRequest, ApiResult<AdminDelegationAvailabilityWindow>>
{
    public override void Configure()
    {
        Post("/admin/countries/{countryId:int}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        CreateDelegationAvailabilityWindowRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var countryId = Route<int>("countryId");
        await Send.OkAsync(ApiResult<AdminDelegationAvailabilityWindow>.Ok(
            await service.CreateWindowAsync(actorId, countryId, req, ct)), ct);
    }
}

public sealed class ListDelegationAvailabilityWindowsEndpoint(IDelegationAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>>
{
    public override void Configure()
    {
        Get("/admin/countries/{countryId:int}/availability-windows");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>.Ok(
            await service.ListWindowsAsync(Route<int>("countryId"), ct)), ct);
}

public sealed class DeleteDelegationAvailabilityWindowEndpoint(IDelegationAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/delegation-availability-windows/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.Manage),
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

/// <summary>Bi-Meeting rework — the free meeting slots for a delegation (a requester
/// from another delegation reads these before requesting a delegation meeting). Empty
/// when the delegation has no future windows.</summary>
public sealed class GetDelegationAvailableSlotsEndpoint(IDelegationAvailabilityService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<DelegationAvailableSlot>>>
{
    public override void Configure()
    {
        Get("/app/countries/{countryId:int}/available-slots");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Programme");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<DelegationAvailableSlot>>.Ok(
            await service.GetAvailableSlotsAsync(Route<int>("countryId"), ct)), ct);
}
