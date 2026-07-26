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

    /// <summary>A18 (2026-07-26, revised 2026-07-27) — the machine-checkable
    /// sentinel that marks stub-produced content a human must not sign off.
    ///
    /// <para>This provider deliberately does NOT emit it. The same provider
    /// answers the visitor chatbot / FAQ / translate, and a reviewer instruction
    /// has no business inside a chat bubble; the flag that travels instead is
    /// <see cref="AiProviderResponse.IsStub"/>. The publish-gated
    /// session-summary desk stamps this sentinel onto the draft it stores (see
    /// <c>AdminSessionSummaryService</c>) and searches for it again before
    /// approve / publish, so it must stay stable and must never appear in real
    /// provider output.</para></summary>
    public const string StubMarker = "[AI-STUB-DO-NOT-PUBLISH]";

    /// <summary>The prefix every stub answer opens with when a model name is set
    /// (<c>"[echo:echo] "</c>). Content stored BEFORE <see cref="StubMarker"/>
    /// existed carries only this, so the summary desk treats a leading occurrence
    /// as stub text too.</summary>
    public const string EchoModelPrefix = "[echo:";

    /// <summary>The prefix every stub answer opens with when no model is set
    /// (<c>"[echo] "</c>). Companion to <see cref="EchoModelPrefix"/>.</summary>
    public const string EchoPrefix = "[echo]";

    public AiProvider Tag => AiProvider.Echo;

    public Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var prefix = call.Model.Length > 0
            ? $"{EchoModelPrefix}{call.Model}] "
            : EchoPrefix + " ";
        var output = prefix + call.UserPrompt;
        // Truncate to the requested max output tokens — 4 chars ≈ 1 token.
        var charCap = Math.Max(8, call.MaxOutputTokens * 4);
        if (output.Length > charCap)
        {
            output = output[..charCap];
        }
        var inputTokens = ApproxTokens(call.SystemPrompt) + ApproxTokens(call.UserPrompt);
        var outputTokens = ApproxTokens(output);
        // IsStub is the signal — not the text. See AiProviderResponse.
        return Task.FromResult(
            new AiProviderResponse(output, inputTokens, outputTokens, IsStub: true));
    }

    private static int ApproxTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
}
