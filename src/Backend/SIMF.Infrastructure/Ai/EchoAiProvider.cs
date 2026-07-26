using SIMF.Application.Ai.Abstractions;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-176 (gap doc G12) — offline deterministic provider.
/// Returns a synthetic response derived from the user prompt so dev
/// + tests never hit the network. Production code paths can leave
/// <see cref="AiProvider.Echo"/> on a prompt to "mute" it without
/// disabling the feature outright.</summary>
internal sealed class EchoAiProvider : IAiProvider
{
    /// <summary>The sentinel model name every prompt is seeded with. When the
    /// effective provider is real, <see cref="AiService"/> treats it (like a blank
    /// model) as "use the provider's configured DefaultModel" — see
    /// <c>AiService.ResolveModelForCall</c>.</summary>
    public const string ModelName = "echo";

    /// <summary>A18 (2026-07-26) — the machine-checkable sentinel every stub
    /// answer opens with. The stub used to emit a bare <c>[echo:model] </c>
    /// prefix, which reads like an internal tag: a reviewer could take an
    /// echoed session-summary draft for real minutes and publish it verbatim.
    /// Consumers that must never ship stub text (see
    /// <c>AdminSessionSummaryService.EnsureNotStubContent</c>) search for this
    /// exact string, so it must stay stable and must never appear in real
    /// provider output.</summary>
    public const string StubMarker = "[AI-STUB-DO-NOT-PUBLISH]";

    /// <summary>The bilingual banner prepended to every stub answer. Written so
    /// a reviewer reading either language sees at a glance that the text is not
    /// model output.</summary>
    private const string StubBanner =
        StubMarker
        + " NOT REAL AI OUTPUT — produced by the offline stub provider; "
        + "it only echoes the prompt. Do not review, approve or publish it. "
        + "ليست مخرجات ذكاء اصطناعي حقيقية — من المزوّد التجريبي غير المتصل؛ "
        + "لا تراجعها أو توافق عليها أو تنشرها.\n";

    public AiProvider Tag => AiProvider.Echo;

    public Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var prefix = call.Model.Length > 0
            ? $"[echo:{call.Model}] "
            : "[echo] ";
        var echoed = prefix + call.UserPrompt;
        // Truncate to the requested max output tokens — 4 chars ≈ 1 token. The
        // cap applies to the echoed body only, so a small MaxOutputTokens can
        // never truncate the A18 banner away.
        var charCap = Math.Max(8, call.MaxOutputTokens * 4);
        if (echoed.Length > charCap)
        {
            echoed = echoed[..charCap];
        }
        var output = StubBanner + echoed;
        var inputTokens = ApproxTokens(call.SystemPrompt) + ApproxTokens(call.UserPrompt);
        var outputTokens = ApproxTokens(output);
        return Task.FromResult(new AiProviderResponse(output, inputTokens, outputTokens));
    }

    private static int ApproxTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
}
