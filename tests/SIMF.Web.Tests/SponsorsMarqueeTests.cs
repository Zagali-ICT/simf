// Tests: SIMF.Web SponsorsFeed + SponsorsMarquee — the shared, live-backend
// sponsors band used by the landing and /partners (replaces the old hardcoded
// STARTIME placeholder list). Covers the flatten + tier-label mapping, the
// degrade-to-empty contract on a failed/unreachable envelope, and the
// component's render: a card per sponsor (×2 for the marquee loop) + tier pill,
// the optional "view all" CTA, and the "empty roster renders nothing" guard.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Sponsors;
using SIMF.Web.Components.Layout;
using SIMF.Web.Content;

namespace SIMF.Web.Tests;

public sealed class SponsorsMarqueeTests : WebComponentTestBase
{
    public SponsorsMarqueeTests()
    {
        // Rtl (LocalizedText) reads CurrentUICulture; pin en-US so the English
        // assertions below hold on an ar-* configured runner.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ---- SponsorsFeed.LoadAsync -----------------------------------------

    [Fact]
    public async Task LoadAsync_flattens_tier_groups_in_order_and_maps_tier_labels()
    {
        var api = Client(ApiResult<PublicSponsors>.Ok(new PublicSponsors(new[]
        {
            new PublicSponsorTierGroup(10, "Platinum", new[]
            {
                Sponsor("SAMI", "الشركة السعودية للصناعات العسكرية", 10, "Platinum"),
            }),
            new PublicSponsorTierGroup(20, "Gold", new[]
            {
                Sponsor("RSNF", "القوات البحرية الملكية السعودية", 20, "Gold"),
            }),
        })));

        var cards = await SponsorsFeed.LoadAsync(api);

        Assert.Equal(2, cards.Count);
        // Preserves the API's highest-tier-first order.
        Assert.Equal("الشركة السعودية للصناعات العسكرية", cards[0].Name.Ar);
        Assert.Equal("SAMI", cards[0].Name.En);
        Assert.Equal("راعٍ بلاتيني", cards[0].Tier.Ar);
        Assert.Equal("Gold sponsor", cards[1].Tier.En);
    }

    [Fact]
    public async Task LoadAsync_returns_empty_when_the_envelope_fails()
    {
        var api = Client(ApiResult<PublicSponsors>.Fail(new ApiError
        {
            Code = "SERVER_ERROR",
            Message = "The sponsors could not be loaded.",
            MessageArabic = "تعذّر تحميل الرعاة.",
        }));

        Assert.Empty(await SponsorsFeed.LoadAsync(api));
    }

    [Fact]
    public async Task LoadAsync_returns_empty_when_the_envelope_has_no_groups()
    {
        // A malformed / partial envelope can deserialize Groups to null despite the
        // non-nullable contract; the feed must degrade to the empty band (its
        // documented contract), not throw. Regression for DEF-003.
        var api = Client(ApiResult<PublicSponsors>.Ok(new PublicSponsors(null!)));

        Assert.Empty(await SponsorsFeed.LoadAsync(api));
    }

    // ---- SponsorsMarquee component --------------------------------------

    [Fact]
    public void Renders_a_card_per_sponsor_with_its_tier_pill()
    {
        var cut = RenderComponent<SponsorsMarquee>(p => p
            .Add(m => m.Items, new[]
            {
                new SponsorCard(new("راعٍ أ", "Sponsor A"), new("راعٍ بلاتيني", "Platinum sponsor")),
                new SponsorCard(new("راعٍ ب", "Sponsor B"), new("راعٍ ذهبي", "Gold sponsor")),
            }));

        Assert.NotEmpty(cut.FindAll(".ln-spon"));
        // Each sponsor is emitted twice (the seamless -50% marquee loop).
        Assert.Equal(4, cut.FindAll(".ln-scard2").Count);
        Assert.Contains("Sponsor A", cut.Markup);
        Assert.Contains("Platinum sponsor", cut.Markup);
        // The old placeholder must never come back.
        Assert.DoesNotContain("Startime", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_the_view_all_cta_only_when_a_href_is_given()
    {
        var items = new[] { new SponsorCard(new("راعٍ", "Sponsor"), new("راعٍ ذهبي", "Gold sponsor")) };

        var withCta = RenderComponent<SponsorsMarquee>(p => p
            .Add(m => m.Items, items)
            .Add(m => m.ViewAllHref, "/partners"));
        Assert.Contains("/partners", withCta.Markup);

        var withoutCta = RenderComponent<SponsorsMarquee>(p => p.Add(m => m.Items, items));
        Assert.DoesNotContain("ln-btn--outline", withoutCta.Markup);
    }

    [Fact]
    public void Renders_nothing_when_the_roster_is_empty()
    {
        var cut = RenderComponent<SponsorsMarquee>(p => p
            .Add(m => m.Items, Array.Empty<SponsorCard>()));

        Assert.Empty(cut.FindAll(".ln-spon"));
    }

    private static PublicSponsor Sponsor(string en, string ar, int tier, string tierName) =>
        new(Guid.NewGuid(), en, ar, tier, tierName, LogoRelativePath: null, Url: null, DisplayOrder: 0);

    private static SimfPublicClient Client(ApiResult<PublicSponsors> response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("https://api.test/") });

    private sealed class StubHandler(ApiResult<PublicSponsors> response) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(response, Web), Web)),
            });
    }
}
