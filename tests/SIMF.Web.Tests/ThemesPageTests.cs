// Tests: bUnit coverage for the public Website /about/themes page
// (Themes.razor + Themes.razor.cs — Figma 5865-35289). The page is static (no
// API): the reusable LandingPageHero (3-level breadcrumb) + an ln-themex theme
// explorer whose five panels reuse the landing's Themes data, paired with the
// page's ordinal tab labels. The tests pin: the single <h1> + 3-level
// breadcrumb, the five tabs + five panels (index-aligned with Landing.Themes),
// the first tab/panel active by default, and the reuse of the landing theme text.
//
// bUnit renders WITHOUT the head script, so `.ln-js` is absent — i.e. the tests
// see the no-JS DOM (all panels present; CSS, not markup, hides the inactive
// ones under JS). That is exactly the graceful-degradation contract to pin.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class ThemesPageTests : WebComponentTestBase
{
    public ThemesPageTests()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_one_h1_hero_with_a_three_level_breadcrumb()
    {
        var cut = RenderComponent<Themes>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Themes.Hero.Title", h1s[0].TextContent);

        Assert.Contains("PageHero.Home", cut.Markup);
        Assert.Contains("About.Breadcrumb", cut.Markup);
        Assert.Contains("Themes.Breadcrumb", cut.Markup);
        Assert.Contains("href=\"/about\"", cut.Markup);
    }

    [Fact]
    public void Renders_five_tabs_and_five_panels_with_the_first_active()
    {
        var cut = RenderComponent<Themes>();

        var tabs = cut.FindAll(".ln-themex__tab");
        var panels = cut.FindAll(".ln-themex__panel");
        Assert.Equal(5, tabs.Count);
        Assert.Equal(5, panels.Count);

        // exactly the first tab + first panel are active by default
        Assert.Single(cut.FindAll(".ln-themex__tab.is-active"));
        Assert.Single(cut.FindAll(".ln-themex__panel.is-active"));
        Assert.Contains("is-active", tabs[0].ClassList);
        Assert.Contains("is-active", panels[0].ClassList);

        // ARIA wiring: first tab selected, each tab controls its panel by id
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));
        Assert.Equal("ln-theme-panel-0", tabs[0].GetAttribute("aria-controls"));
    }

    [Fact]
    public void Panels_reuse_the_landing_theme_text_and_tabs_use_ordinal_labels()
    {
        var cut = RenderComponent<Themes>();

        // ordinal tab labels (this page's own content, EN under en-US)
        Assert.Contains("Theme 1", cut.Markup);
        Assert.Contains("Theme 5", cut.Markup);

        // panels reuse the landing's Themes (title + desc), single-sourced
        var firstTheme = Landing.Themes[0];
        Assert.Contains(firstTheme.Title.For(false), cut.Markup);   // EN title
        Assert.Contains("Themes.OnThisPage", cut.Markup);
    }
}
