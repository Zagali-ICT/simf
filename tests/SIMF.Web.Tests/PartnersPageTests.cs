// Tests: bUnit coverage for the public Website /partners page
// (Partners.razor — Figma 5866-40017). The page has NO API dependency: it renders
// the shared LandingPageHero (no breadcrumb — Partners is a single-page cluster)
// + the landing's two reused showcase bands from single-sourced static content
// (Landing.PartnerLogos + Landing.Sponsors). The tests pin: the single <h1> hero
// contract (Routes.razor focuses it) with no breadcrumb, the reused ln-pband
// government-partner grid (4 cards), and the reused ln-spon sponsors carousel
// (8 cards, arrows) WITHOUT the self-referential "View all" CTA (deviation (a),
// web/partners.md §7).
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection (its indexer returns the key unchanged). The
// pass-through localizer from the base emits each resx key verbatim; the reused
// Landing.PartnerLogos / Landing.Sponsors content is real AR/EN text, so with the
// culture pinned to en-US the English strings are asserted directly.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class PartnersPageTests : WebComponentTestBase
{
    public PartnersPageTests()
    {
        // Pin the culture: the reused Bilingual content resolves .For(rtl) off
        // CurrentUICulture, so the English assertions below would false-fail on
        // an ar-* configured runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /partners is static (no SimfPublicClient). The shared LandingShell
        // resolves @Assets from a ResourceAssetCollection; an empty one returns
        // each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void Renders_one_h1_hero_with_no_breadcrumb_and_info_pills()
    {
        var cut = RenderComponent<Partners>();

        // Routes.razor focuses the single <h1>; the page must render exactly one.
        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Partners.Hero.Title", h1s[0].TextContent);

        // Partners is a single-page cluster → the hero omits the breadcrumb.
        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));

        // two event info-pills (venue + date) reuse the shared landing keys
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
        Assert.Contains("Landing.Hero.Venue", cut.Markup);
        Assert.Contains("Landing.Subnav.Date", cut.Markup);
    }

    [Fact]
    public void Reuses_the_landing_government_partners_band()
    {
        var cut = RenderComponent<Partners>();

        // one ln-pband band, reusing the landing's Partners.* section keys
        Assert.Single(cut.FindAll(".ln-pband"));
        Assert.Contains("Landing.Partners.Title", cut.Markup);
        Assert.Contains("Landing.Partners.Desc", cut.Markup);

        // the four government-entity cards come from the single-sourced
        // Landing.PartnerLogos list (real EN labels under en-US)
        Assert.Equal(4, cut.FindAll(".ln-pcard").Count);
        Assert.Contains("Presidency of State Security", cut.Markup);
        Assert.Contains("Ministry of Interior", cut.Markup);
    }

    [Fact]
    public void Reuses_the_landing_sponsors_carousel_without_a_view_all_cta()
    {
        var cut = RenderComponent<Partners>();

        // one ln-spon carousel, reusing the landing's Sponsors.* keys
        Assert.Single(cut.FindAll(".ln-spon"));
        Assert.Contains("Landing.Sponsors.Title", cut.Markup);

        // the sponsor cards come from the single-sourced Landing.Sponsors list;
        // the tier tag is real content ("Host" under en-US)
        Assert.Equal(8, cut.FindAll(".ln-scard2").Count);
        Assert.Contains("Host", cut.Markup);

        // prev/next carousel arrows render (landing.js scrolls them)
        Assert.Equal(2, cut.FindAll(".ln-spon__arrow").Count);

        // deviation (a), web/partners.md §7 — the landing's "View all" CTA is
        // omitted here: this page IS the full listing, so a self-referential CTA
        // would lead nowhere. Guard the omission against a future copy-paste.
        Assert.Empty(cut.FindAll(".ln-spon .ln-btn--outline"));
    }
}
