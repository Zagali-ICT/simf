namespace SIMF.Common.Enums;

/// <summary>The AI provider a prompt routes to.
/// <see cref="Echo"/> is the deterministic offline provider used by
/// dev + tests; it never makes outbound calls. The others are
/// outbound HTTP providers with working implementations:
/// <see cref="OpenAi"/> (also serves an on-prem OpenAI-compatible
/// endpoint via a local <c>BaseUrl</c>), <see cref="Anthropic"/>
/// and <see cref="Gemini"/> (the Google Generative Language
/// API). <see cref="AzureOpenAi"/> remains a placeholder the service
/// registers but throws <c>AI_PROVIDER_NOT_CONFIGURED</c> against
/// until wired. New values are appended: the enum contract permits
/// additive values that do not reorder existing ones.</summary>
public enum AiProvider
{
    Echo = 0,
    OpenAi = 1,
    AzureOpenAi = 2,
    Anthropic = 3,

    /// <summary>Google Gemini via the Generative Language API
    /// (<c>generativelanguage.googleapis.com</c>). An additive value, which
    /// the enum contract allows. Used for non-sensitive features under the
    /// hybrid policy; sensitive defense content stays on-prem via
    /// <see cref="OpenAi"/> with a local <c>BaseUrl</c>.</summary>
    Gemini = 4,
}
