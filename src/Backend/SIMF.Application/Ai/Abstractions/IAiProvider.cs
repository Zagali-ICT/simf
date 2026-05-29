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

/// <summary>D-176 — provider response.</summary>
public sealed record AiProviderResponse(
    string OutputText, int? TokensInput, int? TokensOutput);
