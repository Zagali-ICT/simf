// CP Phase-2 — behaviour test for the shared SimfTabs primitive: renders the
// tab buttons, marks/disables the active one, and raises ActiveChanged when an
// inactive tab is clicked.
using Bunit;
using SIMF.Components.Layout;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SimfTabsTests : CpComponentTestBase
{
    private static List<SimfTabs.Tab> TwoTabs() => new()
    {
        new("a", "Alpha"),
        new("b", "Beta"),
    };

    [Fact]
    public void Renders_tabs_and_marks_the_active_one()
    {
        var cut = RenderComponent<SimfTabs>(p => p
            .Add(x => x.Tabs, TwoTabs())
            .Add(x => x.Active, "b"));

        Assert.Equal(2, cut.FindAll("button.simf-tabs__tab").Count);
        var active = cut.Find("button.simf-tabs__tab--active");
        Assert.Equal("Beta", active.TextContent.Trim());
        Assert.Equal("true", active.GetAttribute("aria-selected"));
        Assert.True(active.HasAttribute("disabled"));   // the active tab is not re-clickable
    }

    [Fact]
    public void Clicking_an_inactive_tab_raises_ActiveChanged()
    {
        string? changed = null;
        var cut = RenderComponent<SimfTabs>(p => p
            .Add(x => x.Tabs, TwoTabs())
            .Add(x => x.Active, "a")
            .Add(x => x.ActiveChanged, (string k) => changed = k));

        // "Beta" is the inactive tab (index 1) — clicking it reports the new key.
        cut.FindAll("button.simf-tabs__tab")[1].Click();
        Assert.Equal("b", changed);
    }
}
