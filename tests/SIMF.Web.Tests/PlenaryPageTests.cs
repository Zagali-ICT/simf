// Tests: bUnit coverage for the public Website /programme/sessions page
// (Plenary.razor — Figma 5867-22842). Static (no API): the reusable
// LandingPageHero (no breadcrumb) + the landing's ln-sessions / ln-scard family
// rendering the shared Landing.Sessions data. The tests pin: the single <h1>
// with no breadcrumb, the three reused session cards, and the "explore" CTA
// linking to the live agenda.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class PlenaryPageTests : WebComponentTestBase
{
    public PlenaryPageTests()
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
        var cut = RenderComponent<Plenary>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Plenary.Hero.Title", h1s[0].TextContent);

        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Reuses_the_landing_session_cards()
    {
        var cut = RenderComponent<Plenary>();

        Assert.Contains("Plenary.Section.Title", cut.Markup);
        // three ln-scard cards, one per shared Landing.Sessions entry
        var cards = cut.FindAll(".ln-scard");
        Assert.Equal(Landing.Sessions.Count, cards.Count);
        Assert.Equal(3, cards.Count);
        // the reused landing content (EN titles) renders
        Assert.Contains(Landing.Sessions[0].Title.For(false), cut.Markup);
        Assert.Contains(Landing.Sessions[2].Title.For(false), cut.Markup);
    }

    [Fact]
    public void Each_card_cta_links_to_the_live_agenda()
    {
        var cut = RenderComponent<Plenary>();

        var ctas = cut.FindAll(".ln-scard__btn");
        Assert.Equal(3, ctas.Count);
        Assert.All(ctas, a => Assert.Equal("/programme", a.GetAttribute("href")));
    }
}
