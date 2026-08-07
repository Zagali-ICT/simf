// Tests: SIMF.Api.Tests/AiFeatureTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Ai;

/// <summary>Live-stream AI endpoints. Each
/// chunk-translate / chunk-sign-language POST processes one segment
/// of an in-progress transcript and returns a single translated /
/// signed segment. The Flutter app calls these on every mic
/// segment from the on-device speech-to-text. A true SignalR hub
/// implementation lands in a follow-up commit once
/// Microsoft.AspNetCore.SignalR is added to the API project (the
/// Infrastructure freeze rule blocks unilateral package adds —
/// CLAUDE.md §1.7).</summary>
public sealed class LiveTranslateChunkEndpoint(IAiService service)
    : Endpoint<LiveTranslateChunkRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/live-translation/chunk");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(LiveTranslateChunkRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "live-translation",
            new Dictionary<string, string>
            {
                ["text"] = req.Text ?? string.Empty,
                ["sourceLang"] = req.SourceLang ?? "en",
                ["targetLang"] = req.TargetLang ?? "ar",
                ["sessionId"] = req.SessionId ?? string.Empty,
            },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}

public sealed class LiveSignChunkEndpoint(IAiService service)
    : Endpoint<LiveSignChunkRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/live-sign-language/chunk");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(LiveSignChunkRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "live-sign-language",
            new Dictionary<string, string>
            {
                ["text"] = req.Text ?? string.Empty,
                ["sessionId"] = req.SessionId ?? string.Empty,
            },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}
