// Every CpNavigation icon name must be one SimfIcon actually knows.
//
// SimfIcon THROWS ArgumentException on an unknown name, and the side navigation
// renders on every Control Panel page — so one wrong name is not a missing glyph
// on one screen, it is a render exception on the entire Control Panel. That is
// not hypothetical: 4ba30020 added the Statistics nav row with Icon "chart-bar"
// where the set contains "bar-chart", and the browser sweep found it throwing on
// 92 of 97 pages. It is invisible on screen (Blazor recovers and the rest of the
// page renders) and shows up only in the console, which is why it survived a
// manual pass and a full unit suite.
//
// The icon set is private to the component, so the names are read out of the
// .razor source. Reflection would be tidier, but a private static dictionary in a
// generated component type is not reliably reachable, and this reads the one
// place that cannot be out of date with the renderer.
using System.Text.RegularExpressions;
using SIMF.ControlPanel;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpNavigationIconTests
{
    private static readonly Regex IconKey =
        new(@"^\s*\[""(?<name>[a-z0-9-]+)""\]\s*=", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Every_nav_icon_name_is_one_SimfIcon_knows()
    {
        var known = KnownIconNames();
        Assert.True(
            known.Count > 10,
            $"Only {known.Count} icon names were parsed out of SimfIcon.razor — the "
            + "dictionary's shape must have changed, so this guard is no longer "
            + "reading anything. Fix the parser before trusting a pass.");

        var unknown = CpNavigation.Groups
            .SelectMany(group => group.Items)
            .Where(item => !string.IsNullOrWhiteSpace(item.Icon))
            .Where(item => !known.Contains(item.Icon!))
            .Select(item => $"  {item.LabelKey} -> Icon \"{item.Icon}\"")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unknown.Count == 0,
            "A CpNavigation item names an icon SimfIcon does not have. SimfIcon "
            + "throws on an unknown name and the nav renders on EVERY page, so this "
            + "is a render exception across the whole Control Panel, visible only "
            + "in the browser console.\n"
            + string.Join('\n', unknown)
            + "\n\nKnown names: " + string.Join(", ", known.OrderBy(n => n, StringComparer.Ordinal)));
    }

    private static HashSet<string> KnownIconNames()
    {
        var path = Path.Combine(
            FindRepoRoot(), "src", "Shared", "SIMF.Components", "SimfIcon.razor");
        var source = File.ReadAllText(path);
        return [.. IconKey.Matches(source).Select(m => m.Groups["name"].Value)];
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
