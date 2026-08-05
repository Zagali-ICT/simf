// Tests: SIMF.Api.Tests/CpAssistantEndpointTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>The Control Panel operator assistant. Answers an admin's "where is
/// the screen for X / how do I configure Y" and cites the exact CP route. Routed
/// through the one centralised <see cref="IAiService"/> (the <c>cp-assistant</c>
/// prompt) so it inherits provider routing, invocation logging, PII redaction and
/// the input caps. The grounding (<see cref="CpAssistantRequest.Pages"/>) is built
/// server-side by the Control Panel from its navigation catalogue, filtered to the
/// pages the caller may open — so the answer never points at a forbidden page.</summary>
public sealed class CpAssistantEndpoint(IAiService service)
    : Endpoint<CpAssistantRequest, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/admin/ai/assistant");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Assistant.Use),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Every call hits the AI provider, so bound the cost per operator
        // (partitioned on the JWT `sub`), not per IP — the same guard the AI
        // prompt dry-run uses. Per-IP "auth" cannot stop a shared office or an
        // IP-rotating botnet from running up provider spend.
        Options(rb => rb.RequireRateLimiting("ai-assistant"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CpAssistantRequest req, CancellationToken ct)
    {
        var question = req.Question?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AiInputInvalid, 400,
                "A question is required.",
                "السؤال مطلوب.");
        }

        var userId = User.ActorIdOrNull();
        var caller = new AiCallerContext(userId, "Admin");

        var result = await service.InvokeAsync(
            "cp-assistant",
            new Dictionary<string, string>
            {
                ["question"] = question,
                ["locale"] = string.IsNullOrWhiteSpace(req.Locale) ? "en" : req.Locale,
                ["pages"] = req.Pages ?? string.Empty,
            },
            caller, ct);
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(result), ct);
    }
}
