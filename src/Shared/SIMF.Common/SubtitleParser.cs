using System.Text.RegularExpressions;

namespace SIMF.Common;

/// <summary>
/// D-578 — parses a subtitle file (SubRip <c>.srt</c> or WebVTT <c>.vtt</c>), or
/// already-plain text, into a clean readable transcript for the AI session-summary
/// drafter (<c>AdminSessionSummaryService</c> feeds it as the <c>transcript</c> input).
/// Strips cue indices, timestamp lines, the WebVTT header + NOTE/STYLE/REGION blocks
/// and inline cue tags (<c>&lt;c&gt;</c>, <c>&lt;v Speaker&gt;</c>, <c>&lt;00:00:01.000&gt;</c>),
/// collapses blank runs, and drops consecutive duplicate lines (rolling auto-captions
/// repeat the window). Pure + deterministic (no I/O) so it runs identically in the
/// Control Panel (in-process, server-side) and in unit tests.
/// </summary>
public static class SubtitleParser
{
    // A cue timestamp line always carries the "-->" arrow (srt uses ',', vtt '.').
    private static readonly Regex TimestampLine = new("-->", RegexOptions.Compiled);

    // Inline cue tags: <c>, </c>, <v Roger>, <00:00:01.000>, <i>, <b>, …
    private static readonly Regex CueTags = new("<[^>]*>", RegexOptions.Compiled);

    // A bare SubRip cue index line — just an integer on its own line.
    private static readonly Regex IndexLine = new(@"^\d+$", RegexOptions.Compiled);

    // Collapses runs of whitespace left after stripping inline tags to a single space.
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    // WebVTT block keywords — matched only as a standalone block start (the whole
    // line, or the keyword followed by whitespace), so a real caption line like
    // "NOTE: the following is classified" is NOT mistaken for a metadata block.
    private static readonly string[] BlockKeywords = { "WEBVTT", "NOTE", "STYLE", "REGION" };

    /// <summary>Converts subtitle / plain text to a single clean transcript string.
    /// Never throws — unrecognised input degrades to trimmed, tag-stripped text.</summary>
    public static string Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new List<string>(lines.Length);
        var skipMetadataBlock = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            // WebVTT header + metadata blocks run until the next blank line — skip them.
            if (IsBlockKeyword(line))
            {
                skipMetadataBlock = true;
                continue;
            }
            if (line.Length == 0)
            {
                skipMetadataBlock = false;
                continue;
            }
            if (skipMetadataBlock)
            {
                continue;
            }

            if (TimestampLine.IsMatch(line) || IndexLine.IsMatch(line))
            {
                continue;
            }

            var text = WhitespaceRun.Replace(CueTags.Replace(line, string.Empty), " ").Trim();
            if (text.Length == 0)
            {
                continue;
            }

            // Rolling auto-captions repeat the previous line as the window scrolls.
            if (kept.Count > 0 && string.Equals(kept[^1], text, StringComparison.Ordinal))
            {
                continue;
            }

            kept.Add(text);
        }

        return string.Join(" ", kept).Trim();
    }

    /// <summary>True when the line is a WebVTT block start (`WEBVTT`/`NOTE`/`STYLE`/
    /// `REGION`) — the whole line equals the keyword, or the keyword is followed by
    /// whitespace. Punctuation after the keyword (e.g. "NOTE:") is NOT a block start,
    /// so a genuine caption line keeps its text.</summary>
    private static bool IsBlockKeyword(string line)
    {
        foreach (var keyword in BlockKeywords)
        {
            if (line.Equals(keyword, StringComparison.Ordinal))
            {
                return true;
            }
            if (line.Length > keyword.Length
                && line.StartsWith(keyword, StringComparison.Ordinal)
                && char.IsWhiteSpace(line[keyword.Length]))
            {
                return true;
            }
        }
        return false;
    }
}
