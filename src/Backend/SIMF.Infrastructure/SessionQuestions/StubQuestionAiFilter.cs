// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
using SIMF.Application.SessionQuestions.Abstractions;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// P4.2 — D-236: the stub question AI filter shipped in this increment. It does
/// NOT call any AI provider — it returns a deterministic "clean" advisory
/// verdict. The real moderation call (via
/// the central <c>IAiService</c> prompt catalogue) replaces THIS class behind
/// the unchanged <see cref="IQuestionAiFilter"/> interface, with no change to
/// the submit service or its tests.
///
/// <para>Because the question filter is advisory only (it never sets a status —
/// every question lands Pending for the Committee regardless), the stub has no
/// approve/hold decision to configure; it simply records that no real model
/// ran. A deployment wiring the real filter swaps the DI registration in
/// <c>DependencyInjection.cs</c> from this class to the provider-backed one.</para>
/// </summary>
internal sealed class StubQuestionAiFilter : IQuestionAiFilter
{
    public Task<QuestionAiVerdict> ScreenAsync(
        Guid sessionId,
        Guid userId,
        string text,
        CancellationToken cancellationToken = default) =>
        // Deterministic, offline, no provider call. The bucket makes it clear in
        // the audit trail / Committee queue that no real model ran.
        Task.FromResult(new QuestionAiVerdict("stub-clean", Flagged: false));
}
