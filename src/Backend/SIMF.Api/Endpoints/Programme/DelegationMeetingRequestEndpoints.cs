// Tests: SIMF.Api.Tests/DelegationMeetingRequestsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>D-478 (#11, Group G phase 2) — a delegate submits a request for their
/// delegation to meet another invited country's delegation ("count X meets country
/// Y"). Login-required (approved account); 403 when the caller is not a delegate;
/// 400 when the target country is not an invited delegation.</summary>
public sealed class SubmitDelegationMeetingRequestEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<SubmitDelegationMeetingRequestRequest, ApiResult<DelegationMeetingRequestSubmitted>>
{
    public override void Configure()
    {
        Post("/app/delegation-meeting-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Delegates");
    }
    public override async Task HandleAsync(SubmitDelegationMeetingRequestRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<DelegationMeetingRequestSubmitted>.Ok(
            await service.SubmitAsync(actorId, req, ct)), ct);
    }
}

// -- Admin delegation-meeting-request management --

public sealed class ListAdminDelegationMeetingRequestsEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminDelegationMeetingRequestRow>>>
{
    public override void Configure()
    {
        Post("/admin/delegation-meeting-requests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<GridPage<AdminDelegationMeetingRequestRow>>.Ok(
            await service.ListAllAsync(actorId, req, ct)), ct);
    }
}

// Admin opens one record (with the requester email) before the respond modal.
public sealed class GetAdminDelegationMeetingRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class GetAdminDelegationMeetingRequestEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<GetAdminDelegationMeetingRequestRoute, ApiResult<AdminDelegationMeetingRequestDetail>>
{
    public override void Configure()
    {
        Get("/admin/delegation-meeting-requests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Rate-limit the per-record PII drill-down (mirrors the speaker desk).
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetAdminDelegationMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationMeetingRequestDetail>.Ok(
            await service.GetAsync(actorId, req.Id, ct)), ct);
    }
}

// Bi-Meeting rework — the other party (a target-delegation member) confirms an
// Approved (AwaitingSpeaker) meeting from the app by tapping their notification.
public sealed class ConfirmDelegationMeetingRoute
{
    public Guid Id { get; set; }
}

public sealed class ConfirmDelegationMeetingEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<ConfirmDelegationMeetingRoute, ApiResult<AdminDelegationMeetingRequestDetail>>
{
    public override void Configure()
    {
        Post("/app/delegation-meeting-requests/{id:guid}/confirm");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Delegates");
    }
    public override async Task HandleAsync(ConfirmDelegationMeetingRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationMeetingRequestDetail>.Ok(
            await service.ConfirmByOtherPartyAsync(actorId, req.Id, ct)), ct);
    }
}

public sealed class RespondToDelegationMeetingRequestRoute : RespondToDelegationMeetingRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToDelegationMeetingRequestEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<RespondToDelegationMeetingRequestRoute, ApiResult<AdminDelegationMeetingRequestDetail>>
{
    public override void Configure()
    {
        Put("/admin/delegation-meeting-requests/{id:guid}/respond");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(RespondToDelegationMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationMeetingRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}

// Bi-Meeting rework — an operator checks a confirmed meeting in at the hall → Done.
public sealed class CheckInDelegationMeetingRoute
{
    public Guid Id { get; set; }
}

public sealed class CheckInDelegationMeetingEndpoint(IDelegationMeetingRequestService service)
    : Endpoint<CheckInDelegationMeetingRoute, ApiResult<AdminDelegationMeetingRequestDetail>>
{
    public override void Configure()
    {
        Post("/admin/delegation-meeting-requests/{id:guid}/check-in");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.DelegationMeetings.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CheckInDelegationMeetingRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminDelegationMeetingRequestDetail>.Ok(
            await service.CheckInAsync(actorId, req.Id, ct)), ct);
    }
}
