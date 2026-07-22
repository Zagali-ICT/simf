namespace SIMF.Contracts.Ai;

/// <summary>The centralised AI-call input caps, in one shared place so every
/// producer of an AI input references the same numbers instead of hand-copying
/// them. The API's <c>AiService</c> enforces these on every call; the Control
/// Panel grounding builder sizes its page directory against
/// <see cref="MaxInputValueLength"/> so it can never exceed the cap.</summary>
public static class AiInputLimits
{
    /// <summary>Maximum number of input key/value pairs per AI call.</summary>
    public const int MaxInputsCount = 16;

    /// <summary>Maximum length of an input key, in characters.</summary>
    public const int MaxInputKeyLength = 64;

    /// <summary>Maximum length of an input value, in characters.</summary>
    public const int MaxInputValueLength = 4000;
}
