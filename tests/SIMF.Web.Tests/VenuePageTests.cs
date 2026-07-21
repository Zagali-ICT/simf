// Tests: bUnit coverage for the public Website /about/venue page
// (Venue.razor — Figma 5866-40935). Static (no API): the reusable LandingPageHero
// (3-level breadcrumb) + a single venue-info card on the ln-fsection chrome. The
// tests pin the single <h1> + 3-level breadcrumb, and the venue card (name reused
// from the shared event-fact key, date/time, and an external directions link).
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class VenuePageTests : WebComponentTestBase
{
    public VenuePageTests()
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
        var cut = RenderComponent<Venue>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Venue.Hero.Title", h1s[0].TextContent);

        Assert.Contains("PageHero.Home", cut.Markup);
        Assert.Contains("About.Breadcrumb", cut.Markup);
        Assert.Contains("Venue.Breadcrumb", cut.Markup);
        Assert.Contains("href=\"/about\"", cut.Markup);
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_venue_card_reusing_the_shared_event_facts()
    {
        var cut = RenderComponent<Venue>();

        Assert.Single(cut.FindAll(".ln-venue"));
        // the venue name + date + time reuse the shared landing event-fact keys
        Assert.Contains("Landing.Hero.Venue", cut.Markup);
        Assert.Contains("Landing.Subnav.Date", cut.Markup);
        Assert.Contains("Landing.Subnav.Time", cut.Markup);
    }

    [Fact]
    public void Directions_link_opens_an_external_map_safely()
    {
        var cut = RenderComponent<Venue>();

        var link = cut.Find(".ln-venue a.ln-btn");
        Assert.Contains("google.com/maps", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        // external target must not leak the opener
        Assert.Contains("noopener", link.GetAttribute("rel"));
    }
}
