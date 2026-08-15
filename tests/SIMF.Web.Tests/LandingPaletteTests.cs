// Tests: the landing palette regression. An automated "raw hex -> design token"
// rewrite (commit f81ba630, shipped to production via PR 300) rewrote the token
// DEFINITION block of landing.css along with the use sites, so all 38 colour
// tokens came out as `--navy: var(--navy)`. A custom property that references
// itself is a dependency cycle: CSS resolves it to the guaranteed-invalid value,
// every `var(--navy)` use site then falls back to the inherited or initial value,
// and the entire public site rendered with transparent backgrounds and black
// text. Measured in Chrome on the shipped stylesheet: --navy computed to "",
// .ln-hero background to rgba(0, 0, 0, 0).
//
// Nothing caught it. The stylesheet parses, the Release build is clean, and the
// 140 Website tests all render markup rather than colour. The convention checker
// reported a SUCCESS (165 -> 98 findings) because its SIMF-N2 rule counts raw hex
// at USE sites, and deleting a palette removes every one of them.
//
// These are ratchet tests: each one FAILS on the pre-fix tree and holds after, so
// a future sweep that empties the palette breaks the build instead of shipping a
// colourless site. The companion guard is SIMF-N3 in tool/conventions, which fails
// CI on the same shape anywhere in the repo.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class LandingPaletteTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string LandingCssPath = "src/Website/SIMF.Web/wwwroot/css/landing.css";

    /// <summary>A CSS block comment. Stripped before analysis so prose describing
    /// the banned shape is not mistaken for the shape itself.</summary>
    private static readonly Regex CssComment = new(
        @"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>`--name: value;` — a custom-property definition.</summary>
    private static readonly Regex TokenDefinition = new(
        @"^\s*--(?<name>[\w-]+)\s*:\s*(?<value>[^;]+);", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>`var(--name)` — a use site.</summary>
    private static readonly Regex TokenUse = new(
        @"var\(\s*--(?<name>[\w-]+)", RegexOptions.Compiled);

    /// <summary>The value is a reference to the property being defined. Covers the
    /// fallback form as well: `var(--navy, #001640)` reads as defensive but is not,
    /// because a var() inside a cycle invalidates the whole property rather than
    /// falling back, so the colour is gone either way.</summary>
    private static bool IsSelfReference(string name, string value) =>
        Regex.IsMatch(value.Trim(), @"^var\(\s*--" + Regex.Escape(name) + @"\s*[,)]");

    /// <summary>Supplied per-element by the markup rather than by the stylesheet:
    /// Organizer.razor sets `style="--logo:url('/…')"` on each partner tile, which
    /// is the sanctioned way to pass a runtime value into CSS (the same carve-out
    /// SIMF-N1 makes). A stylesheet definition would be wrong here, so this token
    /// is legitimately used without one.</summary>
    private static readonly string[] MarkupSuppliedTokens = ["logo"];

    [Fact]
    public void No_landing_token_is_defined_as_a_reference_to_itself()
    {
        var css = ReadLandingCss();

        var selfReferencing = TokenDefinition.Matches(css)
            .Where(m => IsSelfReference(m.Groups["name"].Value, m.Groups["value"].Value))
            .Select(m => "--" + m.Groups["name"].Value)
            .ToArray();

        Assert.True(
            selfReferencing.Length == 0,
            $"{selfReferencing.Length} token(s) in {LandingCssPath} are defined as their own "
            + "name, which CSS resolves to the guaranteed-invalid value. Every use site of "
            + "these has silently lost its colour: "
            + string.Join(", ", selfReferencing));
    }

    [Fact]
    public void The_brand_colours_hold_their_specified_values()
    {
        var tokens = DefinedTokens();

        // The identity of the brand. Values mirror the Figma variable collection
        // ("KSA Maritime Forum — Home Page", file TIrT0HXNvqIGxU10X8LaFq) and are
        // the ones the refactor destroyed.
        var expected = new Dictionary<string, string>
        {
            ["navy"] = "#001640",
            ["primary"] = "#244a77",
            ["secondary"] = "#498fbd",
            ["gold"] = "#e8c060",
            ["ink"] = "#161616",
            ["white"] = "#ffffff",
        };

        foreach (var (name, value) in expected)
        {
            Assert.True(tokens.ContainsKey(name), $"--{name} is not defined in {LandingCssPath}.");
            Assert.Equal(value, tokens[name], ignoreCase: true);
        }
    }

    [Fact]
    public void Every_colour_token_resolves_to_a_literal()
    {
        var tokens = DefinedTokens();

        // An alias (`--bs-primary: var(--primary)`) is legitimate, so follow the
        // chain rather than demanding a literal at the first hop. What must never
        // happen is a chain that fails to terminate in a real value.
        var unresolved = tokens.Keys
            .Where(name => Resolve(name, tokens, depth: 0) is null)
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "These tokens never resolve to a literal value, so every use site of them "
            + "renders with no colour: " + string.Join(", ", unresolved.Select(n => "--" + n)));
    }

    [Fact]
    public void Every_token_used_in_the_stylesheet_is_defined_by_it()
    {
        var css = ReadLandingCss();
        var defined = DefinedTokens().Keys;

        var undefined = TokenUse.Matches(css)
            .Select(m => m.Groups["name"].Value)
            .Distinct()
            .Where(name => !defined.Contains(name))
            .Where(name => !MarkupSuppliedTokens.Contains(name))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            undefined.Length == 0,
            "These tokens are read but never defined, so they resolve to nothing: "
            + string.Join(", ", undefined.Select(n => "--" + n)));
    }

    /// <summary>Follows an alias chain to the literal at its end, or null when the
    /// chain self-references, dead-ends on an undefined token, or loops.</summary>
    private static string? Resolve(string name, IReadOnlyDictionary<string, string> tokens, int depth)
    {
        if (depth > 16) return null;                       // a cycle longer than one hop
        if (!tokens.TryGetValue(name, out var value)) return null;

        var alias = Regex.Match(value.Trim(), @"^var\(\s*--(?<target>[\w-]+)\s*\)$");
        if (!alias.Success) return value.Trim();           // a literal, or a composite value

        var target = alias.Groups["target"].Value;
        return target == name ? null : Resolve(target, tokens, depth + 1);
    }

    /// <summary>Every custom property the stylesheet defines, comments stripped.</summary>
    private static Dictionary<string, string> DefinedTokens()
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in TokenDefinition.Matches(ReadLandingCss()))
        {
            tokens[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }
        return tokens;
    }

    private static string ReadLandingCss()
    {
        var path = Path.Combine(
            RepoRoot, LandingCssPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"The landing stylesheet is missing: {path}");
        return CssComment.Replace(File.ReadAllText(path), string.Empty);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
    }
}
