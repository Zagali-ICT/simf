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

    public AiProvider Tag => AiProvider.Echo;

    public Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var prefix = call.Model.Length > 0
            ? $"[echo:{call.Model}] "
            : "[echo] ";
        var output = prefix + call.UserPrompt;
        // Truncate to the requested max output tokens — 4 chars ≈ 1 token.
        var charCap = Math.Max(8, call.MaxOutputTokens * 4);
        if (output.Length > charCap)
        {
            output = output[..charCap];
        }
        var inputTokens = ApproxTokens(call.SystemPrompt) + ApproxTokens(call.UserPrompt);
        var outputTokens = ApproxTokens(output);
        return Task.FromResult(new AiProviderResponse(output, inputTokens, outputTokens));
    }

    private static int ApproxTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
}
