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
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpMarkupHygieneTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpComponentsDir = "src/ControlPanel/SIMF.ControlPanel/Components";

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

    // ----------------------------------------------------------------------

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
