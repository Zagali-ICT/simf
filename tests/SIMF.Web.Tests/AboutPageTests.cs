// Tests: bUnit coverage for the public Website /about page
// (About.razor + About.razor.cs — Figma 5865-33963). The page has NO API
// dependency: it renders the shared LandingPageHero + the reusable ln- sections
// from static content (its own Values/Pillars + the landing's reused About /
// Stats data). The tests pin: the single <h1> hero contract (Routes.razor
// focuses it), the four value cards, the reused participation-stats band, and
// the three pillar feature-cards with their distinct icons.
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection (its indexer returns the key unchanged). The
// pass-through localizer from the base emits each resx key verbatim; the
// repeated card content comes from About.razor.cs as real AR/EN text, so with
// the culture pinned to en-US the English strings are asserted directly.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class AboutPageTests : WebComponentTestBase
{
    public AboutPageTests()
    {
        // Pin the culture: the Bilingual card content resolves .For(rtl) off
        // CurrentUICulture, so the English assertions below would false-fail on
        // an ar-* configured runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /about is static (no SimfPublicClient). The shared LandingShell resolves
        // @Assets from a ResourceAssetCollection; an empty one returns each key
        // unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_exactly_one_h1_hero_with_breadcrumb_and_info_pills()
    {
        var cut = RenderComponent<About>();

        // Routes.razor focuses the single <h1>; the page must render exactly one.
        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("About.Hero.Title", h1s[0].TextContent);

        // breadcrumb: shared "Home" crumb + this page's crumb
        Assert.Contains("PageHero.Home", cut.Markup);
        Assert.Contains("About.Breadcrumb", cut.Markup);

        // two event info-pills (venue + date) reuse the shared landing keys
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
        Assert.Contains("Landing.Hero.Venue", cut.Markup);
        Assert.Contains("Landing.Subnav.Date", cut.Markup);
    }

    [Fact]
    public void Renders_the_four_forum_values()
    {
        var cut = RenderComponent<About>();

        Assert.Equal(4, cut.FindAll(".ln-vcard").Count);
        // the labels are real content (About.razor.cs Bilingual), EN under en-US
        // (the "&" in "Integration & communication" is HTML-encoded in markup,
        // so assert on the "&"-free words).
        Assert.Contains("Innovation", cut.Markup);
        Assert.Contains("Integration", cut.Markup);
        Assert.Contains("Sustainability", cut.Markup);
        Assert.Contains("Responsibility", cut.Markup);
    }

    [Fact]
    public void Reuses_the_landing_participation_stats_band()
    {
        var cut = RenderComponent<About>();

        // four canonical participation counters (the shared Landing.Stats source),
        // rendered through the reused ln-stats markup + resx keys.
        Assert.Equal(4, cut.FindAll(".ln-stat").Count);
        Assert.Contains("Landing.Stats.Eyebrow", cut.Markup);
        Assert.Contains("+500", cut.Markup);
        Assert.Contains("Participating countries", cut.Markup);
    }

    [Fact]
    public void Renders_the_three_pillars_with_their_distinct_icons()
    {
        var cut = RenderComponent<About>();

        var cards = cut.FindAll(".ln-fcard");
        Assert.Equal(3, cards.Count);
        Assert.Contains("Strategic dialogue", cut.Markup);
        Assert.Contains("Global partnerships", cut.Markup);
        Assert.Contains("Foreseeing the future", cut.Markup);

        // each pillar carries its own icon asset (not a shared placeholder)
        Assert.Contains("assets/figma/about/icon-globe.svg", cut.Markup);
        Assert.Contains("assets/figma/about/icon-anchor.svg", cut.Markup);
        Assert.Contains("assets/figma/about/icon-chip.svg", cut.Markup);
    }

    [Fact]
    public void Reuses_the_landing_about_intro_block()
    {
        var cut = RenderComponent<About>();

        // the 2-col intro reuses the landing's own About.* content + media,
        // and its CTA points at the landing partners band (the dedicated
        // /partners page lands in Wave 4).
        Assert.Single(cut.FindAll(".ln-about"));
        Assert.Contains("Landing.About.Eyebrow", cut.Markup);
        Assert.Contains("Landing.About.Title", cut.Markup);
        Assert.Contains("href=\"/#partners\"", cut.Markup);
    }
}
