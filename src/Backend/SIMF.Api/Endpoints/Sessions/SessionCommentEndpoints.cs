// Tests: SIMF.Api.Tests/SessionCommentsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.SessionComments.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>D-199 (Mockup page 28 — "Audience comments" / تعليقات
/// الجمهور) — audience submission of a comment on a session. Requires
/// an approved account (an authenticated attendee), mirroring the
/// D-169 question-submission gate — the comment is tied to a signed-in
/// identity, so it is NOT anonymous (unlike the public Delegations /
/// FAQ reads). The body runs through the AI-filter seam (stub now) at
/// the service layer.</summary>
public sealed class SubmitSessionCommentRoute
{
    public Guid SessionId { get; set; }
    public string Body { get; set; } = string.Empty;
}

public sealed class SubmitSessionCommentEndpoint(ISessionCommentService service)
    : Endpoint<SubmitSessionCommentRoute, ApiResult<SessionCommentSubmitted>>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId:guid}/comments");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(SubmitSessionCommentRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.SubmitAsync(
            req.SessionId, userId,
            new SubmitSessionCommentRequest { Body = req.Body },
            ct);
        await Send.OkAsync(ApiResult<SessionCommentSubmitted>.Ok(result), ct);
    }
}

/// <summary>D-199 — the public audience feed for a session: only
/// Approved + active comments, newest first (Mockup page 28). Requires
/// an approved account — the feed is shown inside the authenticated
/// live-stream screen, so it follows the same gate as the submit
/// endpoint rather than being anonymous.</summary>
public sealed class ListSessionCommentsRoute
{
    public Guid SessionId { get; set; }
}

public sealed class ListSessionCommentsEndpoint(ISessionCommentService service)
    : Endpoint<ListSessionCommentsRoute, ApiResult<IReadOnlyList<SessionCommentFeedRow>>>
{
    public override void Configure()
    {
        Get("/sessions/{sessionId:guid}/comments");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
    }

    public override async Task HandleAsync(ListSessionCommentsRoute req, CancellationToken ct)
    {
        var rows = await service.ListApprovedAsync(req.SessionId, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<SessionCommentFeedRow>>.Ok(rows), ct);
    }
}
