using SIMF.Common.Enums;

namespace SIMF.Application.Ai.Abstractions;

/// <summary>D-176 (gap doc G12) — the swappable AI back-end.
/// Implementations: <c>EchoAiProvider</c> (deterministic offline
/// stub for dev + tests) and <c>OpenAiProvider</c> (real HTTP
/// integration; placeholder in this commit). New providers register
/// themselves with their <see cref="AiProvider"/> tag in DI; the
/// service resolves by tag at call time.</summary>
public interface IAiProvider
{
    AiProvider Tag { get; }

    Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default);
}

/// <summary>D-176 — provider call args (after template substitution).</summary>
public sealed record AiProviderCall(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    double Temperature,
    int MaxOutputTokens);

/// <summary>D-176 — provider response.
///
/// <para>A18 (2026-07-27) — <paramref name="IsStub"/> is provider METADATA, not
/// text: it is <c>true</c> only for the offline stub provider, which does not
/// answer a prompt, it echoes it. Consumers that must never ship placeholder
/// content (the session-summary desk) branch on this flag; the visitor-facing
/// features (chatbot / FAQ / translate) ignore it and render
/// <paramref name="OutputText"/> as-is, so reviewer-facing wording is never
/// pushed into a chat bubble.</para></summary>
public sealed record AiProviderResponse(
    string OutputText, int? TokensInput, int? TokensOutput, bool IsStub = false);
