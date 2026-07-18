// Tests: bUnit coverage for the public Website /programme/gov-meetings page
// (GovMeetings.razor — Figma 5867-23988 [stub]). Static (no API): the reusable
// LandingPageHero (no breadcrumb) + a minimal intro card with a
// "register your interest" mailto CTA (the Figma frame was a pure organiser-
// placeholder stub; this page shows real minimal content). The tests pin the
// single <h1> with no breadcrumb and the intro card + CTA.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class GovMeetingsPageTests : WebComponentTestBase
{
    public GovMeetingsPageTests()
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
        var cut = RenderComponent<GovMeetings>();

        var h1s = cut.FindAll("h1");
        Assert.Single(h1s);
        Assert.Contains("GovMeetings.Hero.Title", h1s[0].TextContent);

        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        Assert.Equal(2, cut.FindAll(".ln-pghero__pill").Count);
    }

    [Fact]
    public void Renders_the_intro_card_with_a_register_interest_mailto_cta()
    {
        var cut = RenderComponent<GovMeetings>();

        Assert.Contains("GovMeetings.Section.Title", cut.Markup);
        Assert.Single(cut.FindAll(".ln-venue"));

        var cta = cut.Find(".ln-venue a.ln-btn");
        Assert.Contains("GovMeetings.Cta", cta.TextContent);
        Assert.StartsWith("mailto:info@simforum.mod.gov.sa", cta.GetAttribute("href"));
    }
}
