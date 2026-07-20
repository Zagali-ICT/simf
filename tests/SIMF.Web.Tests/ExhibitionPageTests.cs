// Tests: bUnit coverage for the public Website /programme/exhibition page
// (Exhibition.razor — Figma 5867-23560). Static (no API): the reusable
// LandingPageHero (no breadcrumb) + the exhibition floor-plan map rendered as an
// image in a scrollable card. The tests pin the single <h1> with no breadcrumb
// and the map image (source + accessible alt text).
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class ExhibitionPageTests : WebComponentTestBase
{
    public ExhibitionPageTests()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_one_h1_hero_with_no_breadcrumb()
    {
        var cut = RenderComponent<Exhibition>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Exhibition.Hero.Title", h1s[0].TextContent);

        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_floor_plan_map_image_with_alt_text()
    {
        var cut = RenderComponent<Exhibition>();

        Assert.Contains("Exhibition.Section.Title", cut.Markup);
        var map = cut.Find(".ln-exhibit .ln-exhibit__map");
        Assert.Equal("assets/figma/exhibition/exhibition-map.png", map.GetAttribute("src"));
        // accessible alt (the pass-through localizer emits the key verbatim)
        Assert.Equal("Exhibition.Map.Alt", map.GetAttribute("alt"));
        // the map sits in a horizontally scrollable container
        Assert.Single(cut.FindAll(".ln-exhibit__scroll"));
    }
}
