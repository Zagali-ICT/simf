// Unit cover for the transactional-email SUBJECT substitutor. The bodies are
// HTML and go through EmailTemplateRenderer, which encodes every substituted
// value; a MIME subject header is not HTML, so it has its own renderer.
using SIMF.Infrastructure.Email;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class EmailSubjectRendererTests
{
    private static Dictionary<string, string> Tokens(string visitorName) =>
        new(StringComparer.OrdinalIgnoreCase) { ["VisitorName"] = visitorName };

    [Fact]
    public void Render_does_not_html_encode_the_value()
    {
        // The defect: the exhibitor lead-capture mail arrived titled
        // "… Sa&#39;ad Al-Otaibi &amp; Partners", because the subject went
        // through the HTML body renderer and a mail client renders a header
        // literally. Apostrophes and ampersands are ordinary in transliterated
        // names, so this was the normal case, not an edge one.
        var rendered = EmailSubjectRenderer.Render(
            "SIMF visitor captured at your booth: {VisitorName}",
            Tokens("Sa'ad Al-Otaibi & Partners"));

        Assert.Equal(
            "SIMF visitor captured at your booth: Sa'ad Al-Otaibi & Partners", rendered);
    }

    [Fact]
    public void Render_replaces_a_line_break_in_a_value_with_a_space()
    {
        // Without the encoding, a newline inside a value would terminate the
        // Subject header and let the rest of the value pose as another one.
        var rendered = EmailSubjectRenderer.Render(
            "Lead: {VisitorName}", Tokens("Sam\r\nBcc: attacker@example.com"));

        Assert.Equal("Lead: Sam  Bcc: attacker@example.com", rendered);
        Assert.DoesNotContain("\n", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_leaves_an_unknown_token_literal()
    {
        // Same contract as the body renderer, so the CP's unknown-token check
        // still describes what a send will do.
        var rendered = EmailSubjectRenderer.Render("Hello {Foo}", Tokens("Sam"));

        Assert.Equal("Hello {Foo}", rendered);
    }

    [Fact]
    public void Render_returns_empty_for_an_empty_template()
    {
        Assert.Equal(string.Empty, EmailSubjectRenderer.Render(string.Empty, Tokens("Sam")));
    }
}
