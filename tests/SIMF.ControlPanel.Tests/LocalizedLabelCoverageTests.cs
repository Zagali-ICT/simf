// §6.16 — shared Simf* components declare ENGLISH literal defaults for the
// accessible names of their icon-only controls, and expect the host to pass a
// localized value ("The host supplies it localised" — SimfPasswordField.razor:40).
// Where a call site forgets, the English default silently reaches the Arabic UI.
// Nothing is visibly broken in English, which is why it survived review.
//
// These ratchets close the two cases fixed in this change:
//   • SimfPasswordField — the reveal toggle announced "Show password" on the CP
//     sign-in, reset-password and change-password flows (confirmed on a live
//     Arabic render of /login).
//   • SimfDataGrid with Multiselect — 2 of the 47 multiselect pages omitted
//     SelectAllLabel/SelectRowLabel, so an Arabic toolbar showed the English
//     "Select all" as VISIBLE text.
//
//   • SimfModal — the icon-only X announced "Close" on 59 of 63 CP dialogs,
//     including RequireExplicitClose ones where the X is the ONLY way out.
//   • SimfButton — while Loading, the component replaces its label entirely with
//     a spinner named "Working", so on 62 of 116 buttons the accessible name
//     flipped from the localized label to an English literal mid-action.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class LocalizedLabelCoverageTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpComponentsDir = "src/ControlPanel/SIMF.ControlPanel/Components";

    [Fact]
    public void Every_password_field_supplies_a_localized_show_and_hide_label()
    {
        var offenders = OpeningTags("SimfPasswordField")
            .Where(tag => !tag.Text.Contains("ShowLabel", StringComparison.Ordinal)
                       || !tag.Text.Contains("HideLabel", StringComparison.Ordinal))
            .Select(tag => $"{tag.File}:{tag.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16: these SimfPasswordField usages omit ShowLabel/HideLabel, so the "
            + "reveal toggle announces the English default in the Arabic UI: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_multiselect_grid_supplies_localized_select_labels()
    {
        var offenders = OpeningTags("SimfDataGrid")
            .Where(tag => tag.Text.Contains("Multiselect=\"true\"", StringComparison.Ordinal))
            .Where(tag => !tag.Text.Contains("SelectAllLabel", StringComparison.Ordinal)
                       || !tag.Text.Contains("SelectRowLabel", StringComparison.Ordinal))
            .Select(tag => $"{tag.File}:{tag.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16: these multiselect SimfDataGrid usages omit SelectAllLabel/"
            + "SelectRowLabel, so the Arabic toolbar shows the English \"Select all\": "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_modal_supplies_a_localized_close_label()
    {
        var offenders = OpeningTags("SimfModal")
            // A HideClose modal renders no X at all — a blocking progress overlay
            // with nothing to close to. There is no button left to name.
            .Where(tag => !tag.Text.Contains("HideClose=\"true\"", StringComparison.Ordinal))
            .Where(tag => !tag.Text.Contains("CloseLabel", StringComparison.Ordinal))
            .Select(tag => $"{tag.File}:{tag.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16: these SimfModal usages omit CloseLabel, so the only accessible "
            + "name on the X button is the English default \"Close\": "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_loading_button_supplies_a_localized_loading_label()
    {
        // SimfButton drops its ChildContent entirely while Loading, so LoadingLabel
        // is the button's ONLY accessible name during the action it is performing.
        var offenders = OpeningTags("SimfButton")
            .Where(tag => tag.Text.Contains("Loading=", StringComparison.Ordinal))
            .Where(tag => !tag.Text.Contains("LoadingLabel", StringComparison.Ordinal))
            .Select(tag => $"{tag.File}:{tag.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16: these SimfButton usages pass Loading= without LoadingLabel, so "
            + "mid-action the accessible name becomes the English default \"Working\": "
            + string.Join(", ", offenders));
    }

    // ----------------------------------------------------------------------

    private sealed record OpeningTag(string File, int Line, string Text);

    /// <summary>Every <c>&lt;ComponentName ... &gt;</c> opening tag in the CP, with
    /// its full attribute list. The tag spans several lines, so a line-by-line grep
    /// would miss attributes; and a `[^>]*` regex is WRONG here because Razor
    /// attribute values routinely contain '>' inside a lambda
    /// (RowKey="@(row => row.Id.ToString())"), which truncates the match at the
    /// arrow and makes every later attribute invisible. So scan forward tracking
    /// quote state and stop at the first '>' that is not inside a quoted value.</summary>
    private static IEnumerable<OpeningTag> OpeningTags(string componentName)
    {
        var open = "<" + componentName;

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, CpComponentsDir.Replace('/', Path.DirectorySeparatorChar)),
            "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoRoot, file).Replace(Path.DirectorySeparatorChar, '/');

            for (var start = source.IndexOf(open, StringComparison.Ordinal);
                 start >= 0;
                 start = source.IndexOf(open, start + 1, StringComparison.Ordinal))
            {
                // "<SimfDataGridColumn" must not match "<SimfDataGrid".
                var after = start + open.Length;
                if (after < source.Length && (char.IsLetterOrDigit(source[after]) || source[after] == '_')) continue;

                var quote = '\0';
                var end = -1;
                for (var i = after; i < source.Length; i++)
                {
                    var c = source[i];
                    if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                    if (c is '"' or '\'') { quote = c; continue; }
                    if (c == '>') { end = i; break; }
                }
                if (end < 0) continue;

                yield return new OpeningTag(
                    relative,
                    source.Take(start).Count(c => c == '\n') + 1,
                    source[start..(end + 1)]);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException("Could not locate the SIMF repo root from " + AppContext.BaseDirectory)
            : dir.FullName;
    }
}
