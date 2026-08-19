// Tests: SIMF.Api.Tests/EmailSubjectRendererTests.cs
using System.Text;
using System.Text.RegularExpressions;

namespace SIMF.Infrastructure.Email;

/// <summary>Substitutes the same <c>{Token}</c> placeholders as the shared
/// body renderer, but for the MIME <b>subject header</b> — which is not HTML.
///
/// <para>The subject used to go through the body renderer, which HTML-encodes
/// every substituted value because a body is HTML and the value is untrusted. A
/// header is neither: an exhibitor's lead-capture mail arrived titled
/// <c>Sa&amp;#39;ad Al-Otaibi &amp;amp; Partners</c> because the mail client
/// renders a header literally. Apostrophes and ampersands are ordinary in
/// transliterated names and company names, so this was the normal case and it
/// was visible to an external recipient.</para>
///
/// <para>Dropping the encoding puts the value straight into a header, so this
/// strips CR and LF instead: a newline inside a substituted value would end the
/// Subject header and let the rest of the value pose as another one.</para>
///
/// <para>The ideal home for this is a <c>RenderPlain</c> beside
/// <c>EmailTemplateRenderer.Render</c> in the Application layer, so the token
/// syntax lives in exactly one place.</para></summary>
internal static partial class EmailSubjectRenderer
{
    /// <summary>Mirrors the body renderer's token syntax: one brace pair around
    /// letters, digits and underscore.</summary>
    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    /// <summary>Substitutes each <c>{Token}</c> with its raw (un-encoded,
    /// newline-stripped) value; a token the caller did not supply is left
    /// literal, exactly as the body renderer leaves it.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return TokenRegex().Replace(template, match =>
            tokens.TryGetValue(match.Groups[1].Value, out var value)
                ? StripLineBreaks(value)
                : match.Value);
    }

    private static string StripLineBreaks(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            cleaned.Append(character is '\r' or '\n' ? ' ' : character);
        }

        return cleaned.ToString();
    }
}
