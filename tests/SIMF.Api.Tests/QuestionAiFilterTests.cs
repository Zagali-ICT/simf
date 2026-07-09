using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.Infrastructure.SessionQuestions;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-714 (item 12, GAP-1) — unit tests for the real <see cref="AiQuestionFilter"/>.
/// A fake <see cref="IAiService"/> returns canned model output so the verdict
/// mapping + the safe fallbacks are covered offline (no provider / key). The
/// filter is advisory only — every path returns a verdict and never throws.
/// </summary>
public sealed class QuestionAiFilterTests
{
    private static AiQuestionFilter Filter(string? output, bool throwOnCall = false) =>
        new(new _FakeAiService(output, throwOnCall),
            NullLogger<AiQuestionFilter>.Instance);

    [Fact]
    public async Task Allowed_reply_is_a_clean_unflagged_verdict()
    {
        var verdict = await Filter("{\"allowed\": true, \"reason\": \"on topic\"}")
            .ScreenAsync(Guid.NewGuid(), Guid.NewGuid(), "What is the agenda?");

        Assert.Equal("ai-clean", verdict.Verdict);
        Assert.False(verdict.Flagged);
    }

    [Fact]
    public async Task Rejected_reply_is_a_flagged_verdict()
    {
        var verdict = await Filter("{\"allowed\": false, \"reason\": \"spam\"}")
            .ScreenAsync(Guid.NewGuid(), Guid.NewGuid(), "buy cheap watches");

        Assert.Equal("ai-flagged", verdict.Verdict);
        Assert.True(verdict.Flagged);
    }

    [Fact]
    public async Task Non_json_reply_falls_back_to_unavailable_unflagged()
    {
        // The offline Echo provider echoes the input rather than JSON.
        var verdict = await Filter("Question: buy cheap watches")
            .ScreenAsync(Guid.NewGuid(), Guid.NewGuid(), "buy cheap watches");

        Assert.Equal("ai-unavailable", verdict.Verdict);
        Assert.False(verdict.Flagged);
    }

    [Fact]
    public async Task Empty_reply_falls_back_to_unavailable()
    {
        var verdict = await Filter(string.Empty)
            .ScreenAsync(Guid.NewGuid(), Guid.NewGuid(), "hello");

        Assert.Equal("ai-unavailable", verdict.Verdict);
        Assert.False(verdict.Flagged);
    }

    [Fact]
    public async Task A_provider_failure_never_throws_and_falls_back()
    {
        var verdict = await Filter(null, throwOnCall: true)
            .ScreenAsync(Guid.NewGuid(), Guid.NewGuid(), "hello");

        Assert.Equal("ai-unavailable", verdict.Verdict);
        Assert.False(verdict.Flagged);
    }

    private sealed class _FakeAiService(string? output, bool throwOnCall) : IAiService
    {
        public Task<AiCallResult> InvokeAsync(
            string promptKey,
            IReadOnlyDictionary<string, string> inputs,
            AiCallerContext caller,
            CancellationToken cancellationToken = default)
        {
            if (throwOnCall)
            {
                throw new InvalidOperationException("provider down");
            }
            return Task.FromResult(new AiCallResult(
                Guid.NewGuid(), promptKey, AiFeature.QuestionFilter,
                AiProvider.Echo, "echo", output ?? string.Empty,
                null, null, 1));
        }
    }
}
