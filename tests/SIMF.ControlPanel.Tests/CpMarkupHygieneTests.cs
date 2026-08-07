// Source-scan ratchets for two CP markup defects. Follows the same approach as
// AccessibilityMarkupTests: assert against the .razor sources so a future commit
// that reintroduces the defect fails the build.
//
//   BUG-002 — seven CP list pages carried the middle dot in their <PageTitle>
//             as the double-encoded byte pair C3 82 C2 B7 ("Â·"), i.e. UTF-8
//             read as Latin-1 and re-encoded, so the browser tab rendered
//             "Banners Â· SIMF".
//   BUG-026 — GateOperatorConsole.razor carried style="margin-top:1rem",
//             against the no-inline-styles rule. The five remaining inline
//             styles in the CP are the ACCEPTED pattern: they inject a runtime
//             colour into a CSS custom property, which a static class cannot do.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpMarkupHygieneTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpComponentsDir = "src/ControlPanel/SIMF.ControlPanel/Components";

    private const string CpProjectDir = "src/ControlPanel/SIMF.ControlPanel";

    /// <summary>"Â·" — U+00C2 U+00B7, the mojibake produced by reading a UTF-8
    /// middle dot as Latin-1 and re-encoding it.</summary>
    private const string MojibakeMiddleDot = "Â·";

    /// <summary>The five accepted inline styles: each writes a runtime colour
    /// into a CSS custom property, which no static class can express. Anything
    /// else with a style attribute is a defect.</summary>
    private static readonly string[] RuntimeColourInlineStyleFiles =
    [
        "Pages/Admin/BulkBadgeGenerator.razor",
        "Pages/Admin/PrintBag.razor",
        "Pages/Admin/ThemesList.razor",
        "Pages/Admin/WalkInRegistrationForm.razor",
        "Pages/Admin/WalkInSuccessModal.razor",
    ];

    [Fact]
    public void No_cp_razor_source_contains_the_double_encoded_middle_dot()
    {
        var offenders = EnumerateRazorSources()
            .Where(file => File.ReadAllText(file).Contains(MojibakeMiddleDot, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "BUG-002: these CP pages carry the double-encoded middle dot in their "
            + "markup (use a plain '·'): " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_cp_page_title_separator_is_a_plain_middle_dot()
    {
        var offenders = EnumerateRazorSources()
            .Where(file =>
            {
                var source = File.ReadAllText(file);
                var start = source.IndexOf("<PageTitle>", StringComparison.Ordinal);
                if (start < 0) return false;
                var end = source.IndexOf("</PageTitle>", start, StringComparison.Ordinal);
                if (end < 0) return false;
                var title = source[start..end];
                // Every CP page title is "<something> · SIMF".
                return title.Contains("SIMF", StringComparison.Ordinal)
                    && !title.Contains(" · SIMF", StringComparison.Ordinal);
            })
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "BUG-002: these CP pages do not use ' · SIMF' as their tab title: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Gate_operator_console_spaces_its_action_row_with_a_class_not_an_inline_style()
    {
        var source = ReadSource($"{CpComponentsDir}/Pages/Admin/GateOperatorConsole.razor");

        Assert.DoesNotContain("style=", source, StringComparison.Ordinal);
        Assert.Contains("simf-form__actions--tight", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tight_form_actions_modifier_uses_a_spacing_token()
    {
        // theme.tokens.css is the single source of truth — the modifier must
        // reference a token, never a magic value.
        var css = ReadSource("src/Shared/SIMF.Components/wwwroot/css/simf-components.css");

        Assert.Contains(
            ".simf-form__actions--tight { margin-block-start: var(--space-4); }",
            css, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_accepted_runtime_colour_pages_use_an_inline_style()
    {
        var expected = RuntimeColourInlineStyleFiles
            .Select(relative => $"{CpComponentsDir}/{relative}")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var actual = EnumerateRazorSources()
            .Where(file => File.ReadAllText(file).Contains("style=", StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void No_cp_source_contains_a_double_encoded_character()
    {
        // §6.16 — BUG-002 was the double-encoded middle dot; the same corruption
        // was still present as 91 more sequences across 36 files (an em dash
        // "â€”" x80, "Â§" x9, "â€¦", "â‰¤"), reaching the screen as garbage in
        // grids and View/Delete panes wherever the em dash is the "no value"
        // placeholder. Every one of these starts with Â, Ã or â — none of which
        // occurs legitimately anywhere in the CP sources — so banning the lead
        // characters outright catches the whole family, not just the four seen.
        var offenders = EnumerateCpSources()
            .Where(file => File.ReadAllText(file).AsSpan().IndexOfAny(MojibakeLeadCharacters) >= 0)
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16: these CP sources carry double-encoded (mojibake) characters. "
            + "The file was read as Latin-1 and re-saved as UTF-8; re-save it as UTF-8 "
            + "and restore the real character: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_localization_key_used_in_cp_source_exists_in_both_resx_files()
    {
        // §6.16 — IStringLocalizer returns the KEY NAME when a resource is missing,
        // so a typo or a half-finished rename renders raw developer text on screen.
        // That shipped: the D-219 local-time work renamed 7 keys off their old zoned
        // suffix at the call sites but not in the resx, so /admin/banners headed two
        // columns "Admin.Banners.Col.Start" / ".End", the Sessions and Banners forms
        // labelled their datetime inputs with raw keys, and creating an admin toasted
        // "Admin.Users.Add.Saved". The CP component tests could not catch it because
        // their fake localizer reports every lookup as found — only a source scan can.
        var english = ResxKeys("src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx");
        var arabic = ResxKeys("src/ControlPanel/SIMF.ControlPanel/Resources/Strings.ar.resx");

        var referenced = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in EnumerateCpSources())
        {
            foreach (Match match in LocalizerCall.Matches(File.ReadAllText(file)))
            {
                referenced.TryAdd(match.Groups[1].Value, Relative(file));
            }
        }

        var missingEnglish = referenced.Where(pair => !english.Contains(pair.Key))
            .Select(pair => $"{pair.Key} (used in {pair.Value})").ToList();
        var missingArabic = referenced.Where(pair => english.Contains(pair.Key) && !arabic.Contains(pair.Key))
            .Select(pair => $"{pair.Key} (used in {pair.Value})").ToList();

        Assert.True(missingEnglish.Count == 0,
            "§6.16: these localization keys are used in CP markup but are absent from "
            + "Strings.resx, so the raw key renders on screen: "
            + string.Join("; ", missingEnglish));

        Assert.True(missingArabic.Count == 0,
            "§6.16: these localization keys exist in Strings.resx but are missing from "
            + "Strings.ar.resx, so Arabic admins see the English text or the raw key: "
            + string.Join("; ", missingArabic));
    }

    // ----------------------------------------------------------------------

    /// <summary>The lead characters of a double-encoded (UTF-8 read as Latin-1)
    /// sequence. None of them occurs legitimately in any CP source.</summary>
    private static readonly char[] MojibakeLeadCharacters = ['Â', 'Ã', 'â'];

    /// <summary>Matches a literal localizer lookup — <c>@L["Some.Key"]</c> in markup
    /// and <c>L["Some.Key"]</c> in code-behind.</summary>
    private static readonly Regex LocalizerCall =
        new("""L\[\s*"([^"]+)"\s*\]""", RegexOptions.Compiled);

    /// <summary>Every <c>.razor</c> and <c>.cs</c> source in the CP project, so the
    /// scan covers code-behind (where several of the missing keys lived) as well as
    /// markup. Build output is excluded.</summary>
    private static IEnumerable<string> EnumerateCpSources() =>
        Directory.EnumerateFiles(
                Path.Combine(RepoRoot, CpProjectDir.Replace('/', Path.DirectorySeparatorChar)),
                "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static HashSet<string> ResxKeys(string relativePath)
    {
        var document = XDocument.Parse(ReadSource(relativePath));
        return document.Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateRazorSources() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot, CpComponentsDir.Replace('/', Path.DirectorySeparatorChar)),
            "*.razor", SearchOption.AllDirectories);

    private static string Relative(string absolute) =>
        Path.GetRelativePath(RepoRoot, absolute).Replace(Path.DirectorySeparatorChar, '/');

    private static string ReadSource(string relativePath)
    {
        var absolute = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolute), $"Expected source file not found: {absolute}");
        return File.ReadAllText(absolute);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
