// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>D-269 (Mockup page 20 "Speaker profile") — an authenticated,
/// approved attendee submits a meeting request to a speaker who has opted in
/// (<c>Speaker.AllowsMeetingRequests</c>). Login-required (not anonymous like
/// the speaker reads); 409 when the speaker does not accept meeting
/// requests.</summary>
public sealed class SubmitSpeakerMeetingRequestRoute
{
    public Guid SpeakerId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

public sealed class SubmitSpeakerMeetingRequestEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<SubmitSpeakerMeetingRequestRoute, ApiResult<SpeakerMeetingRequestSubmitted>>
{
    public override void Configure()
    {
        Post("/app/speakers/{speakerId:guid}/meeting-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Speakers");
    }
    public override async Task HandleAsync(SubmitSpeakerMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<SpeakerMeetingRequestSubmitted>.Ok(
            await service.SubmitAsync(req.SpeakerId, actorId,
                new SubmitSpeakerMeetingRequestRequest
                {
                    RequesterName = req.RequesterName,
                    Subject = req.Subject,
                }, ct)), ct);
    }
}

// -- Admin speaker-meeting-request management --

public sealed class ListAdminSpeakerMeetingRequestsEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>
{
    public override void Configure()
    {
        Post("/admin/speaker-meeting-requests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.View),
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
        await Send.OkAsync(ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>.Ok(
            await service.ListAllAsync(actorId, req, ct)), ct);
    }
}

// Admin fetches one record (with requester email) before the respond modal
// opens. Audited as SpeakerMeetingRequest.Viewed (mirrors the session D-185
// per-record PII access signal).
public sealed class GetAdminSpeakerMeetingRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class GetAdminSpeakerMeetingRequestEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<GetAdminSpeakerMeetingRequestRoute, ApiResult<AdminSpeakerMeetingRequestDetail>>
{
    public override void Configure()
    {
        Get("/admin/speaker-meeting-requests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Rate-limit the per-record PII drill-down (mirrors D-185).
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetAdminSpeakerMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(
            await service.GetAsync(actorId, req.Id, ct)), ct);
    }
}

public sealed class RespondToSpeakerMeetingRequestRoute : RespondToSpeakerMeetingRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToSpeakerMeetingRequestEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<RespondToSpeakerMeetingRequestRoute, ApiResult<AdminSpeakerMeetingRequestDetail>>
{
    public override void Configure()
    {
        Put("/admin/speaker-meeting-requests/{id:guid}/respond");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(RespondToSpeakerMeetingRequestRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}
