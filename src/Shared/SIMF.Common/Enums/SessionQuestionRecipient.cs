namespace SIMF.Common.Enums;

/// <summary>
/// D-174 (gap doc G11, Mockup page 26) — the addressee of an
/// audience-submitted session question. The mockup's live-stream
/// screen offers two recipient pills: المتحدث (Speaker) or المضيف
/// (Host). The default for backward compatibility is
/// <see cref="Speaker"/>.
/// </summary>
public enum SessionQuestionRecipient
{
    /// <summary>To the speaker (default — matches the original G6 shape).</summary>
    Speaker = 0,

    /// <summary>To the session host / moderator on stage.</summary>
    Host = 1,
}
