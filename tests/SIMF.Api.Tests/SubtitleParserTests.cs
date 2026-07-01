// D-578 — unit tests for the shared SubtitleParser (SIMF.Common): it turns an
// .srt / .vtt file (or plain text) into a clean transcript for the AI summariser.
using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SubtitleParserTests
{
    [Fact]
    public void Parses_srt_into_running_text()
    {
        const string srt =
            "1\n" +
            "00:00:01,000 --> 00:00:04,000\n" +
            "Good morning, and welcome.\n" +
            "\n" +
            "2\n" +
            "00:00:05,000 --> 00:00:07,000\n" +
            "Today we discuss seabed security.\n";

        Assert.Equal(
            "Good morning, and welcome. Today we discuss seabed security.",
            SubtitleParser.Parse(srt));
    }

    [Fact]
    public void Parses_vtt_stripping_header_notes_and_inline_tags()
    {
        const string vtt =
            "WEBVTT\n" +
            "Kind: captions\n" +
            "Language: en\n" +
            "\n" +
            "NOTE this is a comment\n" +
            "\n" +
            "00:00:01.000 --> 00:00:04.000 align:start position:0%\n" +
            "<c>Hello</c> <00:00:02.000><c> world</c>\n" +
            "\n" +
            "00:00:05.000 --> 00:00:07.000\n" +
            "<v Speaker>Second line</v>\n";

        Assert.Equal("Hello world Second line", SubtitleParser.Parse(vtt));
    }

    [Fact]
    public void Drops_consecutive_duplicate_rolling_caption_lines()
    {
        const string rolling =
            "1\n00:00:01,000 --> 00:00:02,000\nline one\n\n" +
            "2\n00:00:02,000 --> 00:00:03,000\nline one\n\n" +
            "3\n00:00:03,000 --> 00:00:04,000\nline two\n";

        Assert.Equal("line one line two", SubtitleParser.Parse(rolling));
    }

    [Fact]
    public void Passes_plain_text_through_trimmed()
    {
        Assert.Equal("Just plain text.", SubtitleParser.Parse("  Just plain text.  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void Empty_or_blank_input_yields_empty(string? input)
    {
        Assert.Equal(string.Empty, SubtitleParser.Parse(input));
    }
}
