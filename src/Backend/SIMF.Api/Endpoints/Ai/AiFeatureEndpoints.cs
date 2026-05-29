// Tests: SIMF.Api.Tests/AiFeatureTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
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
        Guid? userId = Guid.TryParse(user.FindFirstValue("sub"), out var p) ? p : null;
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
        Post("/ai/question-filter");
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
        Post("/ai/faq");
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

public sealed class AssistanceEndpoint(IAiService service)
    : Endpoint<AssistanceRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/ai/assistance");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("AI");
    }
    public override async Task HandleAsync(AssistanceRequest req, CancellationToken ct)
    {
        var caller = AiCaller.FromUser(User);
        var result = await service.InvokeAsync(
            "assistance",
            new Dictionary<string, string> { ["message"] = req.Message ?? string.Empty },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}

public sealed class TranslateEndpoint(IAiService service)
    : Endpoint<TranslateRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/ai/translate");
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
