namespace SIMF.Common.Enums;

/// <summary>D-176 (gap doc G12) — the AI provider a prompt routes to.
/// <see cref="Echo"/> is the deterministic offline provider used by
/// dev + tests; it never makes outbound calls. The others are
/// outbound HTTP providers — only <see cref="OpenAi"/> has a working
/// implementation in this commit; the rest are placeholders the
/// service registers but throws <c>AI_PROVIDER_NOT_CONFIGURED</c>
/// against until a follow-up commit wires them.</summary>
public enum AiProvider
{
    Echo = 0,
    OpenAi = 1,
    AzureOpenAi = 2,
    Anthropic = 3,
}
