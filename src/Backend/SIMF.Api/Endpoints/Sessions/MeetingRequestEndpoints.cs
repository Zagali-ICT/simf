// Tests: SIMF.Api.Tests/MeetingRequestsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>D-174 (gap doc G11, Mockup page 27) — authenticated audience
/// submits a meeting request during a live session.</summary>
public sealed class SubmitMeetingRequestRoute
{
    public Guid SessionId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

public sealed class SubmitMeetingRequestEndpoint(IMeetingRequestService service)
    : Endpoint<SubmitMeetingRequestRoute, ApiResult<MeetingRequestSubmitted>>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId:guid}/meeting-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }
    public override async Task HandleAsync(SubmitMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<MeetingRequestSubmitted>.Ok(
            await service.SubmitAsync(req.SessionId, actorId,
                new SubmitMeetingRequestRequest
                {
                    RequesterName = req.RequesterName,
                    Subject = req.Subject,
                }, ct)), ct);
    }
}

// -- Admin meeting-request management --

public sealed class ListAdminMeetingRequestsEndpoint(IMeetingRequestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminMeetingRequestRow>>>
{
    public override void Configure()
    {
        Post("/admin/meeting-requests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingRequests.View),
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
        await Send.OkAsync(ApiResult<GridPage<AdminMeetingRequestRow>>.Ok(
            await service.ListAllAsync(actorId, req, ct)), ct);
    }
}

// D-185 — admin fetches one record (with requester email) before the
// respond modal opens. Audited as MeetingRequest.Viewed so SOC can see
// per-record PII access (vs the list scrape signal AdminMeetingRequestsListed).
public sealed class GetAdminMeetingRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class GetAdminMeetingRequestEndpoint(IMeetingRequestService service)
    : Endpoint<GetAdminMeetingRequestRoute, ApiResult<AdminMeetingRequestDetail>>
{
    public override void Configure()
    {
        Get("/admin/meeting-requests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // D-185 (security review-pass): rate-limit the per-record
        // PII drill-down so a compromised admin can't burst-fetch
        // emails between SIEM AI-010-style bulk-view detections.
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetAdminMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminMeetingRequestDetail>.Ok(
            await service.GetAsync(actorId, req.Id, ct)), ct);
    }
}

public sealed class RespondToMeetingRequestRoute : RespondToMeetingRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToMeetingRequestEndpoint(IMeetingRequestService service)
    : Endpoint<RespondToMeetingRequestRoute, ApiResult<AdminMeetingRequestDetail>>
{
    public override void Configure()
    {
        Put("/admin/meeting-requests/{id:guid}/respond");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(RespondToMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminMeetingRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}
