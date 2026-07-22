using System.Text;

namespace SIMF.Contracts.Ai;

/// <summary>Shared text primitive for the AI "grounding" builders — the Control
/// Panel assistant's page directory (<c>CpAssistantDirectory</c>) and the app
/// assistant's event context (<c>AssistanceContextBuilder</c>). Both feed one AI
/// input value, so the assembled text is capped a safe margin below the shared
/// per-input limit and truncated on a whole-line boundary: a fact/route is never
/// half-emitted, and a group header is never left dangling. Keeping the cap + the
/// truncation invariant here means the two builders can never drift.</summary>
public static class AiGroundingText
{
    /// <summary>The maximum grounding-text length: one AI input value
    /// (<see cref="AiInputLimits.MaxInputValueLength"/>) kept a safe margin below
    /// that shared cap, deriving from it so a future cap change carries through.</summary>
    public const int MaxLength = AiInputLimits.MaxInputValueLength - 200;

    /// <summary>Appends <paramref name="header"/> then each of
    /// <paramref name="lines"/> to <paramref name="builder"/>, but only while they
    /// fit under <see cref="MaxLength"/>: the header is written only when it AND its
    /// first line fit (so no dangling header), and each further line only while it
    /// fits (so a line is never half-emitted). Once the cap is reached the rest of
    /// this section is dropped.</summary>
    public static void AppendCappedSection(
        StringBuilder builder, string header, IEnumerable<string> lines)
    {
        var headerWritten = false;
        foreach (var line in lines)
        {
            if (!headerWritten)
            {
                // +1 each for the newline AppendLine adds.
                if (builder.Length + header.Length + 1 + line.Length + 1 > MaxLength)
                {
                    return;
                }
                builder.AppendLine(header);
                headerWritten = true;
            }
            else if (builder.Length + line.Length + 1 > MaxLength)
            {
                return;
            }
            builder.AppendLine(line);
        }
    }
}
