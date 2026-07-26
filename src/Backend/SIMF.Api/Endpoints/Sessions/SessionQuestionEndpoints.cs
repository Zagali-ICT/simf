// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>D-169 (gap doc G6, PDF §2.7.2 + §2.10) — public submission
/// endpoint. P5.1 — D-242 (FR-704): the venue gate is enforced in the service —
/// a HallAttendance arrival when the hall has a geofence (D-240/D-241), else the
/// <see cref="SubmitSessionQuestionRoute.IsAtVenue"/> self-assert fallback.</summary>
public sealed class SubmitSessionQuestionRoute
{
    public Guid SessionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>D-171 (gap doc G7) — the venue self-assert flag. Used as the
    /// gate only when the session's hall has no geofence configured (D-242);
    /// otherwise the authoritative gate is the HallAttendance arrival record.</summary>
    public bool IsAtVenue { get; set; }

    /// <summary>D-174 (gap doc G11, Mockup page 26) — Speaker or Host.</summary>
    public SIMF.Common.Enums.SessionQuestionRecipient Recipient { get; set; }
        = SIMF.Common.Enums.SessionQuestionRecipient.Speaker;
}

public sealed class SubmitSessionQuestionEndpoint(ISessionQuestionService service)
    : Endpoint<SubmitSessionQuestionRoute, ApiResult<SessionQuestionSubmitted>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/questions");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(SubmitSessionQuestionRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.SubmitAsync(
            req.SessionId, userId,
            new SubmitSessionQuestionRequest
            {
                QuestionText = req.QuestionText,
                IsAtVenue = req.IsAtVenue,
                Recipient = req.Recipient,
            },
            ct);
        await Send.OkAsync(ApiResult<SessionQuestionSubmitted>.Ok(result), ct);
    }
}

/// <summary>D-169 — small helper for the per-session moderator
/// authorization check. Caller is authorized when they hold the
/// Administrator role OR they have a row in <c>SessionModerators</c>
/// for the requested session. Used inline by every moderator endpoint
/// — a formal IAuthorizationRequirement is overkill for one policy.</summary>
internal static class SessionModeratorAuth
{
    public static async Task<Guid?> ResolveAuthorizedUserAsync(
        ClaimsPrincipal user,
        Guid sessionId,
        ISessionModerationService moderationService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue("sub"), out var actorId))
        {
            return null;
        }
        if (user.IsInRole(AppRoles.Administrator))
        {
            return actorId;
        }
        if (await moderationService.IsModeratorAsync(sessionId, actorId, ct))
        {
            return actorId;
        }
        return null;
    }
}

public sealed class ListModeratorQueueRoute
{
    public Guid SessionId { get; set; }

    /// <summary>DEF-MOD-002 — the desk tab to read. Omitted (the shipped app's
    /// call shape) returns the working desk: the Committee-approved rows plus the
    /// ones already marked answered. An explicit status returns exactly that
    /// bucket — <c>Hidden</c> is how the desk lists (and can then restore) the
    /// questions it rejected, so a mis-click is no longer permanent.</summary>
    public SIMF.Common.Enums.QuestionStatus? Status { get; set; }
}

public sealed class ListModeratorQueueEndpoint(ISessionModerationService service)
    : Endpoint<ListModeratorQueueRoute, ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>
{
    public override void Configure()
    {
        Get("/app/sessions/{sessionId:guid}/questions/moderate");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
    }

    public override async Task HandleAsync(ListModeratorQueueRoute req, CancellationToken ct)
    {
        var actorId = await SessionModeratorAuth.ResolveAuthorizedUserAsync(
            User, req.SessionId, service, ct);
        if (actorId is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        var rows = await service.ListAsync(req.SessionId, req.Status, ct);
        await Send.OkAsync(
            ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>.Ok(rows), ct);
    }
}

public sealed class HideQuestionRoute
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public bool IsHidden { get; set; }
}

public sealed class HideQuestionEndpoint(ISessionModerationService service)
    : Endpoint<HideQuestionRoute, ApiResult<SessionQuestionModeratorRow>>
{
    public override void Configure()
    {
        Put("/app/sessions/{sessionId:guid}/questions/{questionId:guid}/hide");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(HideQuestionRoute req, CancellationToken ct)
    {
        var actorId = await SessionModeratorAuth.ResolveAuthorizedUserAsync(
            User, req.SessionId, service, ct);
        if (actorId is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        var row = await service.SetHiddenAsync(
            actorId.Value, req.SessionId, req.QuestionId, req.IsHidden, ct);
        await Send.OkAsync(ApiResult<SessionQuestionModeratorRow>.Ok(row), ct);
    }
}

/// <summary>DEF-MOD-001 — the moderator's "تمت الإجابة" mark, persisted. Same
/// shape and gate as <see cref="HideQuestionRoute"/>: the target state travels on
/// the body and the call is idempotent.</summary>
public sealed class SetQuestionAnsweredRoute
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public bool IsAnswered { get; set; }
}

public sealed class SetQuestionAnsweredEndpoint(ISessionModerationService service)
    : Endpoint<SetQuestionAnsweredRoute, ApiResult<SessionQuestionModeratorRow>>
{
    public override void Configure()
    {
        Put("/app/sessions/{sessionId:guid}/questions/{questionId:guid}/answered");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(SetQuestionAnsweredRoute req, CancellationToken ct)
    {
        var actorId = await SessionModeratorAuth.ResolveAuthorizedUserAsync(
            User, req.SessionId, service, ct);
        if (actorId is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        var row = await service.SetAnsweredAsync(
            actorId.Value, req.SessionId, req.QuestionId, req.IsAnswered, ct);
        await Send.OkAsync(ApiResult<SessionQuestionModeratorRow>.Ok(row), ct);
    }
}

public sealed class PushQuestionRoute
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
}

public sealed class PushQuestionEndpoint(ISessionModerationService service)
    : Endpoint<PushQuestionRoute, ApiResult<SessionQuestionModeratorRow>>
{
    public override void Configure()
    {
        Put("/app/sessions/{sessionId:guid}/questions/{questionId:guid}/push");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(PushQuestionRoute req, CancellationToken ct)
    {
        var actorId = await SessionModeratorAuth.ResolveAuthorizedUserAsync(
            User, req.SessionId, service, ct);
        if (actorId is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        var row = await service.PushAsync(
            actorId.Value, req.SessionId, req.QuestionId, ct);
        await Send.OkAsync(ApiResult<SessionQuestionModeratorRow>.Ok(row), ct);
    }
}

public sealed class ReorderQuestionsRoute
{
    public Guid SessionId { get; set; }
    public IList<Guid> OrderedQuestionIds { get; set; } = new List<Guid>();
}

public sealed class ReorderQuestionsEndpoint(ISessionModerationService service)
    : Endpoint<ReorderQuestionsRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/app/sessions/{sessionId:guid}/questions/reorder");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(ReorderQuestionsRoute req, CancellationToken ct)
    {
        var actorId = await SessionModeratorAuth.ResolveAuthorizedUserAsync(
            User, req.SessionId, service, ct);
        if (actorId is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }
        await service.ReorderAsync(actorId.Value, req.SessionId,
            req.OrderedQuestionIds.ToList(), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
