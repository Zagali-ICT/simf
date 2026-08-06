namespace SIMF.Common.Enums;

/// <summary>
/// The addressee of an
/// audience-submitted session question. The live-stream
/// screen offers two recipient pills: المتحدث (Speaker) or المضيف
/// (Host). The default for backward compatibility is
/// <see cref="Speaker"/>.
/// </summary>
public enum SessionQuestionRecipient
{
    /// <summary>To the speaker (the default).</summary>
    Speaker = 0,

    /// <summary>To the session host / moderator on stage.</summary>
    Host = 1,
}
