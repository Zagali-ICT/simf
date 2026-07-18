// Tests: bUnit coverage for the public Website /programme/opening page
// (Opening.razor + Opening.razor.cs — Figma 5867-22242). Static (no API): the
// reusable LandingPageHero (WITHOUT a breadcrumb — the Programme cluster omits
// it) + a dark "overview" grid of highlight cards + a numbered "target
// participants" list. The tests pin: the single <h1> + the absent breadcrumb,
// the eight dark highlight cards, and the nine numbered participant items.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class OpeningPageTests : WebComponentTestBase
{
    public OpeningPageTests()
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
        var cut = RenderComponent<Opening>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Opening.Hero.Title", h1s[0].TextContent);

        // the Programme-cluster hero omits the breadcrumb entirely
        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_eight_overview_highlights_as_dark_cards()
    {
        var cut = RenderComponent<Opening>();

        Assert.Contains("Opening.Overview.Title", cut.Markup);
        var cards = cut.FindAll(".ln-overview__grid .ln-vcard");
        Assert.Equal(8, cards.Count);
        // every overview card uses the dark variant
        Assert.Equal(8, cut.FindAll(".ln-vcard--dark").Count);
        // ("&" is HTML-encoded in markup, so assert on the "&"-free words)
        Assert.Contains("private workshops", cut.Markup);
        Assert.Contains("B2B bilateral meetings", cut.Markup);
    }

    [Fact]
    public void Renders_the_nine_target_participants_numbered_one_to_nine()
    {
        var cut = RenderComponent<Opening>();

        Assert.Contains("Opening.Participants.Title", cut.Markup);
        var items = cut.FindAll(".ln-numitem");
        Assert.Equal(9, items.Count);
        // the visual badges number 1..9 in order
        var nums = cut.FindAll(".ln-numitem__num");
        Assert.Equal("1", nums[0].TextContent.Trim());
        Assert.Equal("9", nums[8].TextContent.Trim());
        Assert.Contains("Government bodies", cut.Markup);
    }
}
