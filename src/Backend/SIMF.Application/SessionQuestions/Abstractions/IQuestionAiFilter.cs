namespace SIMF.Application.SessionQuestions.Abstractions;

/// <summary>
/// P4.2 — D-236 (Completion Programme §6, stage 1 of the Q&amp;A pipeline D-212):
/// the AI advisory filter for audience questions. On every submission the
/// service calls <see cref="ScreenAsync"/> and persists the returned verdict to
/// <c>SessionQuestion.AiFilterVerdict</c>. It is <b>advisory only</b> — it
/// NEVER changes the question's <c>Status</c> (which always lands Pending for
/// the Scientific Committee); the verdict is shown to the Committee in their
/// queue to inform, not replace, the human decision.
///
/// <para><b>Stub in this increment:</b> the shipped impl
/// (<c>StubQuestionAiFilter</c>) is deterministic and calls no AI provider.
/// The real filter (routing
/// through the central <c>IAiService</c> / a <c>question-filter</c> prompt) is
/// wired later behind THIS interface, with no change to the submit service or
/// its tests.</para>
/// </summary>
public interface IQuestionAiFilter
{
    /// <summary>Screen one question body. Returns a short advisory verdict
    /// bucket (e.g. <c>clean</c>, <c>flagged</c>, <c>stub-clean</c>) persisted
    /// for the Committee + audit. Never decides visibility.</summary>
    Task<QuestionAiVerdict> ScreenAsync(
        Guid sessionId,
        Guid userId,
        string text,
        CancellationToken cancellationToken = default);
}

/// <summary>The advisory verdict. <see cref="Verdict"/> is the
/// short bucket persisted to <c>SessionQuestion.AiFilterVerdict</c>;
/// <see cref="Flagged"/> lets the Committee queue highlight questions the
/// filter is unsure about — advisory only, it never hides anything.</summary>
public sealed record QuestionAiVerdict(string Verdict, bool Flagged = false);
