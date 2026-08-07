namespace SIMF.Common.Enums;

/// <summary>
/// Whether an audience question was
/// asked before the session went live (<see cref="Pre"/>) or during it
/// (<see cref="Live"/>). Set at submit time from the session's start. The Q&amp;A
/// pipeline treats both phases the same (AI → Committee → Moderator); the phase
/// is for display/filtering. Int-backed, additive-only discipline.
/// </summary>
public enum QuestionPhase
{
    /// <summary>Submitted before the session started (pre-question).</summary>
    Pre = 0,

    /// <summary>Submitted during/after the session start (live question).</summary>
    Live = 1,
}
