// Tests: bUnit coverage for the public Website /speakers page
// (Speakers.razor + Speakers.razor.cs — Figma 5840-26779). Pins the render
// branches: a populated speaker grid (name + role pill + country), the empty
// state, the failure-degrades-to-empty contract (a null result just leaves the
// grid empty — the page has NO separate error alert), the media-asset photo
// route, and the conditional role-pill / location.
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection (its indexer returns the key unchanged) alongside the
// SimfPublicClient. SimfPublicClient is sealed over HttpClient, so the single
// anonymous read (GET /api/v1/speakers) is driven by a stub handler returning a
// canned ApiResult envelope serialised with the web defaults the client reads.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Programme;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class SpeakersPageTests : WebComponentTestBase
{
    private readonly StubPublicHandler _handler = new();

    public SpeakersPageTests()
    {
        // Pin the culture: DisplayName/LocationName read CurrentUICulture directly
        // (Arabic-preferred in RTL), so the English assertions below would
        // false-fail on an ar-* configured runner. Fixing the culture proves the
        // code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /speakers is anonymous; it injects only SimfPublicClient (+ the
        // localizer from the base). The shared LandingShell resolves @Assets from
        // a ResourceAssetCollection; an empty one returns each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
        Services.AddSingleton(new SimfPublicClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
    }

    [Fact]
    public void Renders_speaker_cards_when_the_public_list_loads()
    {
        _handler.Speakers = ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(new[]
        {
            Speaker("Dr. Sarah Al-Otaibi", "د. سارة العتيبي",
                rank: "Chief Scientist", countryEn: "Saudi Arabia", countryAr: "المملكة العربية السعودية"),
        }));

        var cut = RenderComponent<Speakers>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Dr. Sarah Al-Otaibi", cut.Markup);
            Assert.Contains("Chief Scientist", cut.Markup);
            Assert.Contains("Saudi Arabia", cut.Markup);
            // the event band always renders its theme heading (resx key verbatim)
            Assert.Contains("Speakers.Band.Theme", cut.Markup);
            // the populated branch is NOT the empty state
            Assert.DoesNotContain("Speakers.Empty", cut.Markup);
        });
        Assert.Single(cut.FindAll(".ln-spkcard"));
    }

    [Fact]
    public void Renders_the_empty_state_when_there_are_no_speakers()
    {
        _handler.Speakers = ApiResult<PublicSpeakers>.Ok(
            new PublicSpeakers(Array.Empty<PublicSpeakerSummary>()));

        var cut = RenderComponent<Speakers>();

        cut.WaitForAssertion(() =>
        {
            // the pass-through localizer emits each resx key verbatim
            Assert.Contains("Speakers.Empty", cut.Markup);
        });
        Assert.Empty(cut.FindAll(".ln-spkcard"));
    }

    [Fact]
    public void A_failed_speakers_envelope_degrades_to_the_empty_state()
    {
        // A failed envelope makes the client return null; the page maps that to
        // an empty list (result?.Items ?? []) — there is NO separate error alert,
        // the grid just shows the empty state and never throws.
        _handler.Speakers = ApiResult<PublicSpeakers>.Fail(new ApiError
        {
            Code = "SERVER_ERROR",
            Message = "The speakers could not be loaded.",
            MessageArabic = "تعذر تحميل المتحدثين.",
        });

        var cut = RenderComponent<Speakers>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Speakers.Empty", cut.Markup);
        });
        Assert.Empty(cut.FindAll(".ln-spkcard"));
    }

    [Fact]
    public void Uses_the_media_asset_photo_route_when_the_speaker_has_a_photo_asset()
    {
        var id = Guid.NewGuid();
        _handler.Speakers = ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(new[]
        {
            Speaker("Jane Roe", "جين رو", id: id, hasPhotoAsset: true),
        }));

        var cut = RenderComponent<Speakers>();

        cut.WaitForAssertion(() =>
            Assert.Contains($"/content/assets/SpeakerPhoto/{id}/image", cut.Markup));
    }

    [Fact]
    public void Omits_the_role_pill_and_location_when_the_speaker_has_neither()
    {
        // One speaker with rank + country, one with neither: the second still
        // renders its name (and its photo gradient) but no role pill and no
        // location line — so exactly one of each affordance is present.
        _handler.Speakers = ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(new[]
        {
            Speaker("Dr. Sarah Al-Otaibi", "د. سارة العتيبي",
                rank: "Chief Scientist", countryEn: "Saudi Arabia", countryAr: "المملكة العربية السعودية"),
            Speaker("Mike Constable", "مايك كونستابل", rank: null, countryEn: null, countryAr: null),
        }));

        var cut = RenderComponent<Speakers>();

        cut.WaitForAssertion(() => Assert.Contains("Mike Constable", cut.Markup));
        Assert.Equal(2, cut.FindAll(".ln-spkcard").Count);
        Assert.Single(cut.FindAll(".ln-spkcard__role"));  // only the first speaker has a rank
        Assert.Single(cut.FindAll(".ln-spkcard__loc"));   // only the first speaker has a country
    }

    private static PublicSpeakerSummary Speaker(
        string name, string nameArabic, string? rank = null,
        string? countryEn = null, string? countryAr = null,
        Guid? id = null, bool hasPhotoAsset = false) =>
        new(id ?? Guid.NewGuid(), name, nameArabic, rank,
            CountryId: null, countryEn, countryAr,
            PhotoRelativePath: null, DisplayOrder: 0, hasPhotoAsset);

    // The page makes exactly one anonymous GET (speakers); route every request to
    // the canned envelope, serialising with the web defaults SimfPublicClient reads.
    private sealed class StubPublicHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public ApiResult<PublicSpeakers> Speakers { get; set; } =
            ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(Array.Empty<PublicSpeakerSummary>()));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(Speakers, Web), Web)),
            });
    }
}
