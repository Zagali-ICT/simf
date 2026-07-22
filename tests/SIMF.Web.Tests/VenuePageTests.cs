// Tests: bUnit coverage for the public Website /about/venue page
// (Venue.razor — Figma 5866-40935). The reusable LandingPageHero (3-level
// breadcrumb) + a single venue-info card on the ln-fsection chrome. The tests
// pin the single <h1> + 3-level breadcrumb, and the venue card (name reused from
// the shared event-fact key, time, and an external directions link). The date
// line is config-driven (D-755 — OrganizationProfile dates via ForumDates), so a
// stubbed SimfPublicClient supplies the dates each test needs (mirrors
// LandingHeroTests); with no dates it falls back to the shared resx label.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts;
using SIMF.Contracts.Organization;
using SIMF.Web.Components.Pages;
using Xunit;

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
        RegisterProfile(eventStart: null, eventEnd: null);

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
        RegisterProfile(eventStart: null, eventEnd: null);

        var cut = RenderComponent<Venue>();

        Assert.Single(cut.FindAll(".ln-venue"));
        // the venue name + time reuse the shared landing event-fact keys; with no
        // configured dates the date line falls back to the shared date key too.
        Assert.Contains("Landing.Hero.Venue", cut.Markup);
        Assert.Contains("Landing.Subnav.Date", cut.Markup);
        Assert.Contains("Landing.Subnav.Time", cut.Markup);
    }

    [Fact]
    public void The_venue_date_is_config_driven_from_the_forum_dates()
    {
        // D-755 — when OrganizationProfile carries event dates, the date line shows
        // the shared bilingual range (not the hardcoded resx label), so a CP date
        // edit propagates to /about/venue like it does on Landing/Speakers.
        RegisterProfile(
            eventStart: new DateTime(2026, 11, 23),
            eventEnd: new DateTime(2026, 11, 25));

        var cut = RenderComponent<Venue>();

        var date = cut.Find(".ln-venue__meta dd");
        Assert.Equal("23-25 November 2026", date.TextContent.Trim());
        // the hardcoded resx label is no longer rendered in the date cell
        Assert.DoesNotContain("Landing.Subnav.Date", date.TextContent);
    }

    [Fact]
    public void The_venue_date_falls_back_to_the_resx_label_when_dates_are_unset()
    {
        // A profile with no event dates (or a transient API outage) must not blank
        // the marketing page — the date cell keeps the shared resx label.
        RegisterProfile(eventStart: null, eventEnd: null);

        var cut = RenderComponent<Venue>();

        var date = cut.Find(".ln-venue__meta dd");
        Assert.Equal("Landing.Subnav.Date", date.TextContent.Trim());
    }

    [Fact]
    public void Directions_link_opens_an_external_map_safely()
    {
        RegisterProfile(eventStart: null, eventEnd: null);

        var cut = RenderComponent<Venue>();

        var link = cut.Find(".ln-venue a.ln-btn");
        Assert.Contains("google.com/maps", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        // external target must not leak the opener
        Assert.Contains("noopener", link.GetAttribute("rel"));
    }

    private void RegisterProfile(DateTime? eventStart, DateTime? eventEnd)
    {
        var client = new SimfPublicClient(new HttpClient(new StubHandler(eventStart, eventEnd))
        {
            BaseAddress = new Uri("https://api.test/"),
        });
        Services.AddSingleton(client);
    }

    private sealed class StubHandler(DateTime? eventStart, DateTime? eventEnd) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = ApiResult<OrganizationProfileResponse>.Ok(new OrganizationProfileResponse(
                Name: "SIMF", NameArabic: "الملتقى", Title: "SIMF", TitleArabic: "الملتقى",
                Slogan: null, SloganArabic: null, Bio: null, BioArabic: null,
                Version: null, VersionDate: null, SysVersion: null, ReleaseDate: null,
                EventStartDate: eventStart, EventEndDate: eventEnd, CurrentYear: 2026, Status: "Open",
                LocationText: null, LocationTextArabic: null, Latitude: null, Longitude: null,
                ContactPhone: null, ContactEmail: null, ContactWebsite: null, LiveStreamUrl: null,
                BackgroundVideoUrl: null,
                Social: new SocialLinks(null, null, null, null, null, null, null),
                LogoUrl: null,
                AboutItems: Array.Empty<OrganizationAboutItemDto>(),
                Details: Array.Empty<OrganizationDetailDto>()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(result, Web), Web)),
            });
        }
    }
}
