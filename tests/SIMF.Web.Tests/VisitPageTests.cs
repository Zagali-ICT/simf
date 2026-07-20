// Tests: bUnit coverage for the public Website /visit page
// (Visit.razor — Figma 5867-24636). This ln SSR page SUPERSEDES the old MudBlazor
// visit-entry page at the same route. NO API dependency: the shared LandingPageHero
// (no breadcrumb) + a navy "why visit" band reusing the landing's ln-discover /
// Landing.DiscoverCards + a "travel & visa" section (real eVisa copy in the reused
// ln-about 2-col). The tests pin: the single <h1> hero contract, a clean
// h1->h2->h3 order, the reused navy destinations band, and the travel & visa
// section with its documented placeholder CTA (web/visit.md §7).
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection (its indexer returns the key unchanged). The
// pass-through localizer from the base emits each resx key verbatim; the reused
// Landing.DiscoverCards content is real AR/EN text, so with the culture pinned to
// en-US the English strings are asserted directly.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class VisitPageTests : WebComponentTestBase
{
    public VisitPageTests()
    {
        // Pin the culture: the reused Bilingual content resolves .For(rtl) off
        // CurrentUICulture, so the English assertions below would false-fail on
        // an ar-* configured runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /visit is static (no SimfPublicClient). The shared LandingShell resolves
        // @Assets from a ResourceAssetCollection; an empty one returns each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_one_h1_hero_with_no_breadcrumb_and_info_pills()
    {
        var cut = RenderComponent<Visit>();

        // Routes.razor focuses the single <h1>; the page must render exactly one.
        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Visit.Hero.Title", h1s[0].TextContent);

        // single-page cluster → the hero omits the breadcrumb
        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Reuses_the_landing_destinations_band_on_a_dark_background()
    {
        var cut = RenderComponent<Visit>();

        // one ln-discover band with the dark modifier, its own Why.* copy
        var band = cut.FindAll(".ln-discover.ln-discover--dark");
        Assert.Single(band);
        Assert.Contains("Visit.Why.Title", cut.Markup);

        // the six destination cards come from the single-sourced Landing.DiscoverCards
        Assert.Equal(6, cut.FindAll(".ln-dcard").Count);
        Assert.Contains("AlUla", cut.Markup);
        Assert.Contains("NEOM", cut.Markup);
    }

    [Fact]
    public void Renders_the_travel_and_visa_section_with_a_placeholder_cta()
    {
        var cut = RenderComponent<Visit>();

        // the visa band + its reused ln-about 2-col
        Assert.Single(cut.FindAll(".ln-visa"));
        Assert.Contains("Visit.Visa.Title", cut.Markup);
        Assert.Contains("Visit.Visa.Heading", cut.Markup);
        Assert.Single(cut.FindAll(".ln-visa .ln-about__inner"));

        // the eligible-countries callout: a placeholder <button> (no href/nav) that
        // is described by the countries-list label (web/visit.md §7)
        var btn = cut.Find(".ln-visa-cta button");
        Assert.Equal("button", btn.GetAttribute("type"));
        Assert.Equal("visa-countries-label", btn.GetAttribute("aria-describedby"));
        Assert.Contains("Visit.Visa.Cta", btn.TextContent);
        Assert.NotNull(cut.Find("#visa-countries-label"));

        // clean heading order: h1 (hero) → 2×h2 (why + visa) → h3s (6 cards + visa heading)
        Assert.Equal(2, cut.FindAll("h2").Count);
    }
}
