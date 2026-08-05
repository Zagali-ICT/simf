// Tests: SIMF.Api.Tests/AiFeatureTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Ai;

/// <summary>D-176 (gap doc G12) — six AI feature endpoints. Each
/// resolves to one named prompt key from the AiPrompt catalogue:
/// `question-filter`, `faq-answer`, `assistance`, `translate`. The
/// live-stream features (LiveTranslation + LiveSignLanguage) ship
/// as SignalR hubs (see <c>LiveAiHub.cs</c>).</summary>
internal static class AiCaller
{
    public static AiCallerContext FromUser(ClaimsPrincipal? user)
    {
        if (user is null || !user.Identity?.IsAuthenticated == true)
        {
            return new AiCallerContext(null, "Anonymous");
        }
        Guid? userId = user.ActorIdOrNull();
        var kind = user.IsInRole("Administrator") ? "Admin"
            : user.IsInRole("Moderator") ? "Moderator"
            : user.IsInRole("Staff") ? "Staff"
            : "Visitor";
        return new AiCallerContext(userId, kind);
    }
}

public sealed class FilterQuestionEndpoint(IAiService service)
    : Endpoint<FilterQuestionRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/question-filter");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(FilterQuestionRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "question-filter",
            new Dictionary<string, string> { ["text"] = req.Text ?? string.Empty },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}

public sealed class AskFaqEndpoint(IAiService service)
    : Endpoint<AskFaqRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/faq");
        AllowAnonymous();
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(AskFaqRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "faq-answer",
            new Dictionary<string, string> { ["question"] = req.Question ?? string.Empty },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}

public sealed class AssistanceEndpoint(
    IAiService service, IAssistanceContextBuilder contextBuilder, IAiChatHistoryService history)
    : Endpoint<AssistanceRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/assistance");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(AssistanceRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        // Ground the answer on the live event (sessions / FAQ / booths), resolved
        // server-side — so the assistant cites the real agenda, not model priors.
        var context = await contextBuilder.BuildAsync(ct);
        // Short-term memory: feed the visitor's recent turns so the assistant
        // stays consistent across the conversation (empty for a first message).
        var priorTurns = caller.UserId is Guid memoryUserId
            ? await history.GetRecentContextAsync(memoryUserId, ct)
            : string.Empty;
        var result = await service.InvokeAsync(
            "assistance",
            new Dictionary<string, string>
            {
                ["message"] = req.Message ?? string.Empty,
                ["context"] = context,
                ["history"] = priorTurns,
                ["locale"] = string.IsNullOrWhiteSpace(req.Locale) ? "en" : req.Locale,
            },
            caller, ct);
        // Persist this exchange so the conversation survives navigation/restart
        // and becomes memory for the next message. Best-effort: persistence must
        // never fail the (already computed, billed) answer — log and return it.
        if (caller.UserId is Guid persistUserId && !string.IsNullOrWhiteSpace(req.Message))
        {
            try
            {
                await history.AppendTurnAsync(persistUserId, req.Message, result.OutputText, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(
                    ex, "Failed to persist AI assistant chat turn for {UserId}.", persistUserId);
            }
        }
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}

public sealed class AssistanceHistoryEndpoint(IAiChatHistoryService history)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AiChatTurn>>>
{
    public override void Configure()
    {
        Get("/app/ai/assistance/history");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var turns = await history.GetHistoryAsync(User.ActorId(), ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<AiChatTurn>>.Ok(turns), ct);
    }
}

public sealed class TranslateEndpoint(IAiService service)
    : Endpoint<TranslateRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/app/ai/translate");
        AllowAnonymous();
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(TranslateRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "translate",
            new Dictionary<string, string>
            {
                ["text"] = req.Text ?? string.Empty,
                ["sourceLang"] = req.SourceLang ?? "en",
                ["targetLang"] = req.TargetLang ?? "ar",
            },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}
