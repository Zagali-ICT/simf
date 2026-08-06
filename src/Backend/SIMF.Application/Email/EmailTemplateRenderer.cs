using System.Net;
using System.Text.RegularExpressions;

namespace SIMF.Application.Email;

/// <summary>The <c>{Token}</c> substitutor + bilingual composer shared by
/// the email-template resolver and the CP preview. Token syntax is a single
/// brace pair around a name of letters/digits/underscore: <c>{Code}</c>.
/// Substituted VALUES are HTML-encoded — the template author is trusted to write
/// HTML, the runtime values are not. An unknown token is left verbatim at render
/// time; <see cref="FindUnknownTokens"/> surfaces them so the CP can block a save
/// that references a placeholder the template does not define.</summary>
public static partial class EmailTemplateRenderer
{
    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    /// <summary>Substitutes each <c>{Token}</c> with its HTML-encoded value;
    /// tokens not present in <paramref name="tokens"/> are left literal.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return TokenRegex().Replace(template, match =>
            tokens.TryGetValue(match.Groups[1].Value, out var value)
                ? WebUtility.HtmlEncode(value ?? string.Empty)
                : match.Value);
    }

    /// <summary>Composes the bilingual HTML body: the English block, a rule, then
    /// the Arabic block wrapped right-to-left — the shape every SIMF transactional
    /// email uses.</summary>
    public static string ComposeBody(string bodyEn, string bodyAr) =>
        $"{bodyEn}<hr/><div dir=\"rtl\">{bodyAr}</div>";

    /// <summary>Returns the distinct tokens the template references that are NOT
    /// in <paramref name="known"/> (case-insensitive, first-seen order). An empty
    /// result means the template is safe to save.</summary>
    public static IReadOnlyList<string> FindUnknownTokens(string template, ISet<string> known)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        var unknown = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TokenRegex().Matches(template))
        {
            var name = match.Groups[1].Value;
            if (!known.Contains(name) && seen.Add(name))
            {
                unknown.Add(name);
            }
        }

        return unknown;
    }
}
