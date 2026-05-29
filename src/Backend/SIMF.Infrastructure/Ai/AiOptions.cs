using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-176 (gap doc G12) — bound to <c>Ai</c> section in
/// <c>appsettings.json</c>. Defaults pick the offline
/// <see cref="AiProvider.Echo"/> provider so test + dev runs never
/// reach out to the network. Production overrides with real
/// provider credentials via environment variables.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>The provider tag used when a prompt has
    /// <see cref="AiProvider.Echo"/> AND the operator wants to redirect
    /// the call to a real backend without editing every prompt.
    /// Defaults to <see cref="AiProvider.Echo"/>.</summary>
    public AiProvider DefaultProvider { get; set; } = AiProvider.Echo;

    public OpenAiOptions OpenAi { get; set; } = new();
}

public sealed class OpenAiOptions
{
    /// <summary>API key. Must be supplied via env var
    /// <c>SIMF_Ai__OpenAi__ApiKey</c> in production — never committed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Base URL. Defaults to the public OpenAI endpoint.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Model fallback when a prompt omits its own Model.</summary>
    public string DefaultModel { get; set; } = "gpt-4o-mini";
}
