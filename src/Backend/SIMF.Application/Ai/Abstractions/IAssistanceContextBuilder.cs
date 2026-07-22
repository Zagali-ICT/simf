namespace SIMF.Application.Ai.Abstractions;

/// <summary>Builds the compact "event context" the app AI assistant (the
/// <c>assistance</c> prompt) is grounded on: the live programme sessions, FAQ, and
/// exhibition booths — bilingual, one line each. So the assistant answers from the
/// real event data instead of the model's general knowledge. The result is one AI
/// input value, kept under the shared input-value cap and truncated on a whole-line
/// boundary so a fact is never half-emitted.</summary>
public interface IAssistanceContextBuilder
{
    Task<string> BuildAsync(CancellationToken cancellationToken = default);
}
