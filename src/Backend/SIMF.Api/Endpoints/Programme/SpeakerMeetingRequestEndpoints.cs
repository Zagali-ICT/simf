// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
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

    /// <summary>D-474 (#11) — the picked availability slot (VIP slot flow); null for
    /// a legacy topic-only request.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }
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
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<SpeakerMeetingRequestSubmitted>.Ok(
            await service.SubmitAsync(req.SpeakerId, actorId,
                new SubmitSpeakerMeetingRequestRequest
                {
                    RequesterName = req.RequesterName,
                    Subject = req.Subject,
                    SlotStart = req.SlotStart,
                    SlotEnd = req.SlotEnd,
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
        var actorId = User.ActorId();
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
        var actorId = User.ActorId();
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
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}

// R-1 — admin re-sends the speaker's Approve/Reject confirmation links for a request
// still AwaitingSpeaker (the prior 72h token pair expired, or the email was skipped).
public sealed class ResendSpeakerMeetingConfirmationRoute
{
    public Guid Id { get; set; }
}

public sealed class ResendSpeakerMeetingConfirmationEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<ResendSpeakerMeetingConfirmationRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/speaker-meeting-requests/{id:guid}/resend-confirmation");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(ResendSpeakerMeetingConfirmationRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.ResendSpeakerConfirmationAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// Bi-Meeting rework — an operator checks a confirmed meeting in at the hall → Done.
public sealed class CheckInSpeakerMeetingRoute
{
    public Guid Id { get; set; }
}

public sealed class CheckInSpeakerMeetingEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<CheckInSpeakerMeetingRoute, ApiResult<AdminSpeakerMeetingRequestDetail>>
{
    public override void Configure()
    {
        Post("/admin/speaker-meeting-requests/{id:guid}/check-in");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CheckInSpeakerMeetingRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(
            await service.CheckInAsync(actorId, req.Id, ct)), ct);
    }
}

// QA B20 — an admin reopens a Rejected / Cancelled request back to Pending so a
// mistaken decline or cancel is recoverable. Same Manage permission as the other
// decisions on the page; 409 for any status that still holds a slot.
public sealed class ReopenSpeakerMeetingRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class ReopenSpeakerMeetingRequestEndpoint(ISpeakerMeetingRequestService service)
    : Endpoint<ReopenSpeakerMeetingRequestRoute, ApiResult<AdminSpeakerMeetingRequestDetail>>
{
    public override void Configure()
    {
        Post("/admin/speaker-meeting-requests/{id:guid}/reopen");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(ReopenSpeakerMeetingRequestRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(
            await service.ReopenAsync(actorId, req.Id, ct)), ct);
    }
}
