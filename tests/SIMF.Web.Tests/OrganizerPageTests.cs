// Tests: bUnit coverage for the public Website /about/organizer page
// (Organizer.razor + Organizer.razor.cs — Figma 5865-38003). Static (no API):
// the reusable LandingPageHero (3-level breadcrumb) + two organiser cards on the
// ln-fsection chrome. The tests pin: the single <h1> + 3-level breadcrumb, the
// two organiser cards with real MOD/RSNF content, and the two logo treatments
// (a plain <img> emblem vs the navy-masked forum mark).
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class OrganizerPageTests : WebComponentTestBase
{
    public OrganizerPageTests()
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
        var cut = RenderComponent<Organizer>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Organizer.Hero.Title", h1s[0].TextContent);

        Assert.Contains("PageHero.Home", cut.Markup);
        Assert.Contains("About.Breadcrumb", cut.Markup);
        Assert.Contains("Organizer.Breadcrumb", cut.Markup);
        Assert.Contains("href=\"/about\"", cut.Markup);
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_two_organising_bodies_with_real_content()
    {
        var cut = RenderComponent<Organizer>();

        Assert.Equal(2, cut.FindAll(".ln-orgcard").Count);
        Assert.Contains("Organizer.Section.Title", cut.Markup);
        // real EN content (Organizer.razor.cs Bilingual) — MOD + RSNF
        Assert.Contains("Ministry of Defense", cut.Markup);
        Assert.Contains("Royal Saudi Naval Forces", cut.Markup);
    }

    [Fact]
    public void Uses_an_img_emblem_for_MOD_and_a_navy_masked_mark_for_RSNF()
    {
        var cut = RenderComponent<Organizer>();

        // MOD: a plain colour emblem <img>
        Assert.Contains("assets/figma/organizer/mod-emblem.svg", cut.Markup);
        // RSNF: the forum mark recoloured navy via the mask class (white-on-white fix),
        // fed the asset through the --logo custom property (not hardcoded in CSS).
        // Regression: the url() MUST be ROOT-relative ('/assets/…'). A relative path in
        // a custom property resolves against the stylesheet (/css/…), not <base href>,
        // so it 404s and the RSNF mark renders blank on the nested /about/organizer route.
        var masked = cut.FindAll(".ln-orgcard__logo--masked");
        Assert.Single(masked);
        Assert.Contains("--logo:url('/assets/figma/nav/logo-fill.svg')", masked[0].GetAttribute("style"));
    }
}
