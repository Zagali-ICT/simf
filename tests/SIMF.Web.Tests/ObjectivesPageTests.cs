// Tests: bUnit coverage for the public Website /about/objectives page
// (Objectives.razor + Objectives.razor.cs — Figma 5865-34626). The page is
// static (no API): the reusable LandingPageHero (with a 3-level breadcrumb
// Home / About / Objectives) + six ln-fcard feature cards on the ln-fsection
// layout. The tests pin: the single <h1> + 3-level breadcrumb, and the six
// objectives with their distinct icons + the raised-card variant.
//
// The pass-through localizer from the base emits each resx key verbatim; the
// card content comes from Objectives.razor.cs as real AR/EN text, so with the
// culture pinned to en-US the English strings are asserted directly.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class ObjectivesPageTests : WebComponentTestBase
{
    public ObjectivesPageTests()
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
        var cut = RenderComponent<Objectives>();

        // Routes.razor focuses the single <h1>; the page must render exactly one.
        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Objectives.Hero.Title", h1s[0].TextContent);

        // 3-level breadcrumb: Home / About / Objectives — the middle "About"
        // level is a link back to /about.
        Assert.Contains("PageHero.Home", cut.Markup);
        Assert.Contains("About.Breadcrumb", cut.Markup);
        Assert.Contains("Objectives.Breadcrumb", cut.Markup);
        Assert.Contains("href=\"/about\"", cut.Markup);

        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_six_objectives_as_raised_feature_cards()
    {
        var cut = RenderComponent<Objectives>();

        Assert.Equal(6, cut.FindAll(".ln-fcard").Count);
        // every objective card carries the raised (drop-shadow) variant
        Assert.Equal(6, cut.FindAll(".ln-fcard--raised").Count);

        Assert.Contains("Objectives.Section.Title", cut.Markup);
        // real EN content (Objectives.razor.cs Bilingual)
        Assert.Contains("Strengthening maritime security", cut.Markup);
        Assert.Contains("Energy security", cut.Markup);
        Assert.Contains("International cooperation", cut.Markup);
    }

    [Fact]
    public void Each_objective_card_has_its_own_icon()
    {
        var cut = RenderComponent<Objectives>();

        // six distinct icon assets (not a shared placeholder)
        Assert.Contains("assets/figma/objectives/icon-security.svg", cut.Markup);
        Assert.Contains("assets/figma/objectives/icon-supply.svg", cut.Markup);
        Assert.Contains("assets/figma/objectives/icon-energy.svg", cut.Markup);
        Assert.Contains("assets/figma/objectives/icon-infrastructure.svg", cut.Markup);
        Assert.Contains("assets/figma/objectives/icon-digital.svg", cut.Markup);
        Assert.Contains("assets/figma/objectives/icon-cooperation.svg", cut.Markup);
    }
}
