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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminMeetingRequestRow>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class RespondToMeetingRequestRoute : RespondToMeetingRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToMeetingRequestEndpoint(IMeetingRequestService service)
    : Endpoint<RespondToMeetingRequestRoute, ApiResult<AdminMeetingRequestRow>>
{
    public override void Configure()
    {
        Put("/admin/meeting-requests/{id:guid}/respond");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
        await Send.OkAsync(ApiResult<AdminMeetingRequestRow>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}
