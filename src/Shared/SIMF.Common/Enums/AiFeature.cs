namespace SIMF.Common.Enums;

/// <summary>D-176 (gap doc G12) — the named AI features the SIMF
/// platform exposes. Each feature is wired to one default prompt at
/// seed time (e.g. <see cref="QuestionFilter"/> →
/// <c>question-filter</c>); a feature can have several prompts for
/// A/B testing — the caller passes <c>promptKey</c> explicitly.</summary>
public enum AiFeature
{
    QuestionFilter = 0,
    Faq = 1,
    Assistance = 2,
    Translate = 3,
    LiveTranslation = 4,
    LiveSignLanguage = 5,

    /// <summary>P4.1 — D-238: AI session-summary / محضر drafting (Completion
    /// Programme §6.4.1). Additive value — appended, never reorders the above.</summary>
    SessionSummary = 6,
}
