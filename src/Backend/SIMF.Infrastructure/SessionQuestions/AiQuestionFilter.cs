// Tests: SIMF.Api.Tests/QuestionAiFilterTests.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.SessionQuestions.Abstractions;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// D-714 (item 12, FDS-007 §B.4 GAP-1) — the real audience-question AI filter.
/// Routes through the central <see cref="IAiService"/> and the seeded
/// <c>question-filter</c> prompt (which replies JSON
/// <c>{"allowed": bool, "reason": string}</c>), replacing
/// <see cref="StubQuestionAiFilter"/> behind the unchanged
/// <see cref="IQuestionAiFilter"/> interface.
///
/// <para><b>Advisory only</b>, exactly like the stub: it NEVER changes the
/// question's <c>Status</c> — every question still lands Pending for the
/// Scientific Committee; the verdict only informs the human review.</para>
///
/// <para>Registered in place of the stub ONLY when
/// <c>SessionQuestions:AiFilterEnabled</c> is true (default = stub, so the PoC
/// needs no AI key). Any AI failure — a provider error, a timeout, or a non-JSON
/// reply — falls back to a safe <c>ai-unavailable</c> verdict, so a broken or
/// unconfigured model NEVER blocks a question submission.</para>
/// </summary>
internal sealed class AiQuestionFilter(
    IAiService aiService,
    ILogger<AiQuestionFilter> logger) : IQuestionAiFilter
{
    private const string PromptKey = "question-filter";

    public async Task<QuestionAiVerdict> ScreenAsync(
        Guid sessionId,
        Guid userId,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await aiService.InvokeAsync(
                PromptKey,
                new Dictionary<string, string> { ["text"] = text },
                new AiCallerContext(userId, "Visitor"),
                cancellationToken);
            return ParseVerdict(result.OutputText);
        }
        catch (Exception ex)
        {
            // Advisory filter: a failed call must never break question submission.
            logger.LogWarning(ex,
                "Question AI filter failed for session {SessionId}; verdict falls back to unavailable.",
                sessionId);
            return Unavailable;
        }
    }

    private static readonly QuestionAiVerdict Unavailable =
        new("ai-unavailable", Flagged: false);

    /// <summary>Maps the model's <c>{"allowed": bool, "reason": string}</c> reply
    /// to the advisory bucket. A missing/malformed reply (e.g. the offline Echo
    /// provider echoes the input, not JSON) → <c>ai-unavailable</c> — safe, and it
    /// never raises a flag on noise that could bias the Committee.</summary>
    private static QuestionAiVerdict ParseVerdict(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Unavailable;
        }
        try
        {
            using var doc = JsonDocument.Parse(output);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("allowed", out var allowed)
                && (allowed.ValueKind == JsonValueKind.True
                    || allowed.ValueKind == JsonValueKind.False))
            {
                return allowed.GetBoolean()
                    ? new QuestionAiVerdict("ai-clean", Flagged: false)
                    : new QuestionAiVerdict("ai-flagged", Flagged: true);
            }
        }
        catch (JsonException)
        {
            // Non-JSON reply — advisory only, treat as unavailable.
        }
        return Unavailable;
    }
}
