using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>Bound to <c>Ai</c> section in
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

    /// <summary>The Anthropic (Claude) Messages-API provider settings.</summary>
    public AnthropicOptions Anthropic { get; set; } = new();

    /// <summary>The Google Gemini (Generative Language API) provider settings.</summary>
    public GeminiOptions Gemini { get; set; } = new();

    /// <summary>HMAC key for prompt-content drift hashes.</summary>
    public AiPromptHashOptions PromptHash { get; set; } = new();
}

/// <summary>Anthropic (Claude) provider settings. Bound to
/// <c>Ai:Anthropic</c>; the API key is supplied via env var
/// <c>SIMF_Ai__Anthropic__ApiKey</c> in production — never committed.</summary>
public sealed class AnthropicOptions
{
    /// <summary>API key (<c>sk-ant-…</c>). Empty ⇒ the provider throws
    /// <c>AI_PROVIDER_NOT_CONFIGURED</c> (503).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Messages-API base URL. Defaults to the public Anthropic endpoint.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>Model fallback when a prompt omits its own Model.</summary>
    public string DefaultModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>The required <c>anthropic-version</c> request header.</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary><c>max_tokens</c> fallback (Anthropic requires it on every
    /// request) when a prompt/call does not specify one.</summary>
    public int DefaultMaxTokens { get; set; } = 2048;
}

/// <summary>Google Gemini (Generative Language API) provider settings. Bound to
/// <c>Ai:Gemini</c>; the API key is supplied via env var
/// <c>SIMF_Ai__Gemini__ApiKey</c> in production — never committed. Used for
/// non-sensitive features under the hybrid policy.</summary>
public sealed class GeminiOptions
{
    /// <summary>API key. Empty ⇒ the provider throws
    /// <c>AI_PROVIDER_NOT_CONFIGURED</c> (503).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Generative Language API base URL. Defaults to the public endpoint.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>Model fallback when a prompt omits its own Model (e.g.
    /// <c>gemini-2.5-flash</c>). The per-service model is set in the CP.</summary>
    public string DefaultModel { get; set; } = "gemini-2.5-flash";

    /// <summary><c>maxOutputTokens</c> fallback when a prompt/call does not
    /// specify one.</summary>
    public int DefaultMaxTokens { get; set; } = 2048;
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

/// <summary>D-181 (review-pass hardening of D-179) — HMAC key for
/// <see cref="SIMF.Infrastructure.Ai.AiAuditDetail.PromptContentHash"/>.
/// Raw SHA-256 over `SystemPrompt + 0x1F + UserPromptTemplate` lets an
/// insider with audit-read brute-force short or templated prompts via
/// rainbow tables ("Summarise: {visitorName}" is highly guessable).
/// HMAC-SHA256 keyed on this server-only secret keeps the drift-detection
/// property and adds preimage resistance even against insiders.
/// Provide via env var <c>SIMF_Ai__PromptHashSecret</c>. Rotation
/// changes all hashes — that's acceptable because the audit log is
/// time-bounded and SOC compares hashes within a window, not against
/// historical baselines.</summary>
public sealed class AiPromptHashOptions
{
    /// <summary>Random ≥32-byte ASCII or base64 secret. If empty, the
    /// hasher falls back to a deterministic process-static derivation —
    /// fine for dev / tests, but logs a warning at startup so production
    /// noise surfaces a misconfiguration.</summary>
    public string? Secret { get; set; }
}
