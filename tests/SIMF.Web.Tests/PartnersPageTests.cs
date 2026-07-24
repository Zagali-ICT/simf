// Tests: bUnit coverage for the public Website /partners page
// (Partners.razor + Partners.razor.cs — Figma 5866-40017). The government-partners
// band is static (Landing.PartnerLogos); the sponsors band reuses the shared
// <SponsorsMarquee>, which reads the LIVE roster from the anonymous public API
// (SponsorsFeed → SimfPublicClient). The tests pin: the single <h1> hero contract
// (Routes.razor focuses it) with no breadcrumb, the reused ln-pband government
// grid (4 cards), and the shared backend sponsors marquee (real sponsor cards,
// no STARTIME) WITHOUT the self-referential "View all" CTA (deviation (a)).
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection. SimfPublicClient is sealed over HttpClient, so the
// sponsors read (GET /api/v1/app/sponsors) is driven by a stub handler returning
// a canned ApiResult envelope. Culture pinned to en-US so the EN strings assert.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Sponsors;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class PartnersPageTests : WebComponentTestBase
{
    private readonly StubPublicHandler _handler = new();

    public PartnersPageTests()
    {
        // Pin the culture: the reused Bilingual content resolves .For(rtl) off
        // CurrentUICulture, so the English assertions below would false-fail on
        // an ar-* configured runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // The shared LandingShell resolves @Assets from a ResourceAssetCollection;
        // an empty one returns each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
        // /partners reads its sponsors band live from the public API.
        Services.AddSingleton(new SimfPublicClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
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
    public void Reuses_the_shared_backend_sponsors_marquee_without_a_view_all_cta()
    {
        _handler.Sponsors = ApiResult<PublicSponsors>.Ok(new PublicSponsors(new[]
        {
            new PublicSponsorTierGroup(10, "Platinum", new[]
            {
                Sponsor("SAMI", "الشركة السعودية للصناعات العسكرية", 10, "Platinum"),
            }),
            new PublicSponsorTierGroup(20, "Gold", new[]
            {
                Sponsor("RSNF", "القوات البحرية الملكية السعودية", 20, "Gold"),
            }),
        }));

        var cut = RenderComponent<Partners>();

        // one ln-spon band, reusing the landing's Sponsors.* keys
        Assert.Single(cut.FindAll(".ln-spon"));
        Assert.Contains("Landing.Sponsors.Title", cut.Markup);

        // real backend sponsors render (EN under en-US) as wordmark cards, each
        // emitted twice for the marquee loop → 2 sponsors × 2 = 4 cards
        Assert.Equal(4, cut.FindAll(".ln-scard2").Count);
        Assert.Contains("SAMI", cut.Markup);
        Assert.Contains("Platinum sponsor", cut.Markup);
        // the STARTIME placeholder must never come back
        Assert.DoesNotContain("Startime", cut.Markup, StringComparison.OrdinalIgnoreCase);

        // deviation (a): the dedicated listing omits the self-referential CTA
        Assert.Empty(cut.FindAll(".ln-spon .ln-btn--outline"));
    }

    private static PublicSponsor Sponsor(string en, string ar, int tier, string tierName) =>
        new(Guid.NewGuid(), en, ar, tier, tierName, LogoRelativePath: null, Url: null, DisplayOrder: 0);

    // Routes every request to the canned sponsors envelope, serialised with the
    // web defaults SimfPublicClient reads. Defaults to a one-sponsor roster so the
    // band renders for the hero / partners-band tests that don't set it.
    private sealed class StubPublicHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public ApiResult<PublicSponsors> Sponsors { get; set; } =
            ApiResult<PublicSponsors>.Ok(new PublicSponsors(new[]
            {
                new PublicSponsorTierGroup(10, "Platinum", new[]
                {
                    new PublicSponsor(Guid.NewGuid(), "SAMI", "سامي", 10, "Platinum",
                        LogoRelativePath: null, Url: null, DisplayOrder: 0),
                }),
            }));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(Sponsors, Web), Web)),
            });
    }
}
