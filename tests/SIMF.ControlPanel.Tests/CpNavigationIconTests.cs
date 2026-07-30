// Guards every navigation icon name against SimfIcon's lookup.
//
// SimfIcon THROWS on an unknown name, and it is rendered by the shell's sidebar,
// so a single bad name does not degrade one menu entry — it takes down the whole
// Blazor circuit and the Control Panel renders a blank page. Nothing else
// catches it: the name is a plain string, so it compiles, and every unit test
// that does not render the shell passes.
//
// This has now happened at least twice (see the comment at SimfIcon.razor:118
// about "monitor", and the "clipboard-list" crash found on a live render while
// adding the Reports group). A name is cheap to typo and expensive to miss, so
// it gets a test.
using Bunit;
using SIMF.Components;

namespace SIMF.ControlPanel.Tests;

public sealed class CpNavigationIconTests : TestContext
{
    [Fact]
    public void Every_navigation_icon_name_resolves()
    {
        var missing = new List<string>();

        foreach (var name in CpNavigation.Groups
            .SelectMany(group => group.Items)
            .Select(item => item.Icon)
            .Where(icon => !string.IsNullOrWhiteSpace(icon))
            .Distinct()
            .OrderBy(icon => icon, StringComparer.Ordinal))
        {
            try
            {
                RenderComponent<SimfIcon>(parameters => parameters.Add(p => p.Name, name!));
            }
            catch (ArgumentException)
            {
                missing.Add(name!);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"SimfIcon has no path for: {string.Join(", ", missing)}. "
                + "An unknown icon name throws at render time and blanks the whole "
                + "Control Panel, so add the icon to SimfIcon.razor or use an existing name.");
    }

    [Fact]
    public void A_bogus_icon_name_still_throws()
    {
        // Proves the test above can actually fail: if SimfIcon ever stopped
        // throwing, the guard would silently pass for every bad name.
        Assert.Throws<ArgumentException>(() =>
            RenderComponent<SimfIcon>(parameters =>
                parameters.Add(p => p.Name, "definitely-not-an-icon")));
    }
}
