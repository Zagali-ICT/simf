// Tests: bUnit coverage for the public Website /discover page
// (Discover.razor — Figma 5867-29747). The page has NO API dependency: it renders
// the shared LandingPageHero (no breadcrumb — Discover is a single-page cluster)
// + the landing's reused ln-discover destinations band from single-sourced static
// content (Landing.DiscoverCards + the reused Landing.Discover.Title/Desc). The
// tests pin: the single <h1> hero contract (Routes.razor focuses it) with no
// breadcrumb, a clean h1->h2->h3 heading order, the six reused destination cards,
// and the omission of the self-referential "Explore" CTA (web/discover.md §7).
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

public sealed class DiscoverPageTests : WebComponentTestBase
{
    public DiscoverPageTests()
    {
        // Pin the culture: the reused Bilingual content resolves .For(rtl) off
        // CurrentUICulture, so the English assertions below would false-fail on
        // an ar-* configured runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /discover is static (no SimfPublicClient). The shared LandingShell
        // resolves @Assets from a ResourceAssetCollection; an empty one returns
        // each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_one_h1_hero_with_no_breadcrumb_and_info_pills()
    {
        var cut = RenderComponent<Discover>();

        // Routes.razor focuses the single <h1>; the page must render exactly one.
        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Discover.Hero.Title", h1s[0].TextContent);

        // Discover is a single-page cluster → the hero omits the breadcrumb.
        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));

        // two event info-pills (venue + date) reuse the shared landing keys
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Reuses_the_landing_discover_destinations_band()
    {
        var cut = RenderComponent<Discover>();

        // one ln-discover band, reusing the landing's Discover.* section keys
        Assert.Single(cut.FindAll(".ln-discover"));
        Assert.Contains("Landing.Discover.Title", cut.Markup);
        Assert.Contains("Landing.Discover.Desc", cut.Markup);

        // the six destination cards come from the single-sourced Landing.DiscoverCards
        // list (real EN labels + distance/location metas under en-US)
        var cards = cut.FindAll(".ln-dcard");
        Assert.Equal(6, cards.Count);
        Assert.Contains("AlUla", cut.Markup);
        Assert.Contains("NEOM", cut.Markup);
        Assert.Contains("Madinah Region", cut.Markup);

        // clean heading order: hero <h1> → section <h2> → six card <h3>
        Assert.Single(cut.FindAll("h2"));
        Assert.Equal(6, cut.FindAll(".ln-dcard h3").Count);
    }

    [Fact]
    public void Omits_the_self_referential_explore_cta()
    {
        var cut = RenderComponent<Discover>();

        // deviation, web/discover.md §7 — the landing's "Explore Saudi Arabia" CTA
        // is omitted here: this page IS the discover destination, so a
        // self-referential CTA would lead nowhere. Guard the omission.
        Assert.Empty(cut.FindAll(".ln-discover .ln-btn--outline"));
    }
}
