// Tests: SIMF.Api.Tests/MeetingActionTokenTests.cs
using FastEndpoints;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>The public speaker
/// double-opt-in action links. <c>AllowAnonymous</c>: the speaker is not signed in,
/// so the opaque single-use token in the path is the only credential (an
/// owner-approved AllowAnonymous exception). GET previews WITHOUT consuming
/// (safe for email-link prefetch); POST consumes + applies. An unusable token
/// returns a NEUTRAL 404 (<c>MEETING_ACTION_TOKEN_INVALID</c>) that never leaks
/// whether it was unknown / expired / used / already decided. Rate-limited.</summary>
public sealed class PreviewMeetingActionEndpoint(IMeetingActionTokenService service)
    : EndpointWithoutRequest<ApiResult<MeetingActionPreview>>
{
    public override void Configure()
    {
        Get("/app/meeting-actions/{token}");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("auth"));
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var preview = await service.PreviewAsync(Route<string>("token") ?? string.Empty, ct)
            ?? throw MeetingActionErrors.NeutralInvalid();
        await Send.OkAsync(ApiResult<MeetingActionPreview>.Ok(preview), ct);
    }
}

/// <summary>The uniform neutral error both meeting-action endpoints return for any
/// unusable token — never leaking whether it was unknown / expired / used (§15.7).</summary>
file static class MeetingActionErrors
{
    public static ApiException NeutralInvalid() => new(
        ErrorCodes.MeetingActionTokenInvalid, 404,
        "This link is no longer valid.",
        "لم يعد هذا الرابط صالحاً.");
}

public sealed class ConfirmMeetingActionEndpoint(IMeetingActionTokenService service)
    : EndpointWithoutRequest<ApiResult<MeetingActionOutcome>>
{
    public override void Configure()
    {
        Post("/app/meeting-actions/{token}");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("auth"));
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await service.ApplyAsync(Route<string>("token") ?? string.Empty, ct)
            ?? throw MeetingActionErrors.NeutralInvalid();
        await Send.OkAsync(ApiResult<MeetingActionOutcome>.Ok(outcome), ct);
    }
}
