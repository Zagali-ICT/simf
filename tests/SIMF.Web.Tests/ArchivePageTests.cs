// Tests: bUnit coverage for the public Website /archive page
// (Archive.razor + Archive.razor.cs — Figma 5840-27997). Pins the render
// branches: LIVE edition cards + headline counters from the archive edition list
// (newest-first), the fallback to the landing's static Milestones when the archive
// is hidden / empty / unreachable, and the reused static bands (gallery, session
// cards, speakers collage, the wrapping ln-miles band).
//
// The page wraps its sections in the shared LandingShell, whose <HeadContent>
// uses the @Assets fingerprint helper — so the test registers an (empty)
// ResourceAssetCollection alongside the SimfPublicClient. SimfPublicClient is
// sealed over HttpClient, so the single anonymous read (GET /archive) is driven by
// a stub handler returning a canned ApiResult envelope serialised with the web
// defaults the client reads (mirrors SpeakersPageTests).
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Archive;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class ArchivePageTests : WebComponentTestBase
{
    private readonly StubArchiveHandler _handler = new();

    public ArchivePageTests()
    {
        // Pin the culture: the reused Bilingual content + the edition titles resolve
        // off CurrentUICulture, so the English assertions would false-fail on an
        // ar-* runner. Fixing it proves the code, not the environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /archive is anonymous; it injects only SimfPublicClient (+ the localizer
        // from the base). The shared LandingShell resolves @Assets from a
        // ResourceAssetCollection; an empty one returns each key unchanged.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
        Services.AddSingleton(new SimfPublicClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
    }

    [Fact]
    public void Renders_one_h1_hero_the_static_gallery_and_the_reused_bands()
    {
        _handler.Archive = ApiResult<PublicArchive>.Ok(
            new PublicArchive(Array.Empty<PublicArchiveEdition>()));

        var cut = RenderComponent<Archive>();

        cut.WaitForAssertion(() =>
        {
            var h1s = cut.FindAll("h1");
            Assert.Single(h1s);
            Assert.Contains("Archive.Hero.Title", h1s[0].TextContent);
        });
        // single-page cluster → no breadcrumb
        Assert.Empty(cut.FindAll(".ln-pghero__crumbs"));
        // three headline stat counters + the static highlights gallery (6 photos)
        Assert.Equal(3, cut.FindAll(".ln-stat").Count);
        Assert.Equal(6, cut.FindAll(".ln-gallery__item").Count);
        // the reused session cards + speakers collage
        Assert.Equal(3, cut.FindAll(".ln-scard").Count);
        Assert.Single(cut.FindAll(".ln-speakers__collage"));
    }

    [Fact]
    public void Renders_live_edition_cards_newest_first_with_stats_from_the_latest()
    {
        _handler.Archive = ApiResult<PublicArchive>.Ok(new PublicArchive(new[]
        {
            Edition(2024, "SIMF 2024", "سيمف 2024", attendees: 1200, sessions: 40, speakers: 250),
            Edition(2025, "SIMF 2025", "سيمف 2025", attendees: 1500, sessions: 50, speakers: 300),
        }));

        var cut = RenderComponent<Archive>();

        cut.WaitForAssertion(() =>
        {
            // both editions render, newest-first (2025 before 2024)
            var names = cut.FindAll(".ln-mcard__name");
            Assert.Equal(2, names.Count);
            Assert.Contains("SIMF 2025", names[0].TextContent);
            Assert.Contains("SIMF 2024", names[1].TextContent);
            // the headline counters come from the latest edition (2025)
            Assert.Contains("+300", cut.Markup);
            Assert.Contains("+1500", cut.Markup);
        });
        // the variable-length live band uses the page-scoped wrap modifier
        Assert.Single(cut.FindAll(".ln-miles--wrap"));
    }

    [Fact]
    public void Falls_back_to_the_static_past_editions_when_the_archive_is_unreachable()
    {
        // A failed envelope makes the client return null; the page maps that to an
        // empty list and falls back to the landing's static Milestones — it never
        // throws and never blanks.
        _handler.Archive = ApiResult<PublicArchive>.Fail(new ApiError
        {
            Code = "SERVER_ERROR",
            Message = "The archive could not be loaded.",
            MessageArabic = "تعذر تحميل الأرشيف.",
        });

        var cut = RenderComponent<Archive>();

        cut.WaitForAssertion(() =>
        {
            // the static fallback renders the landing's three past editions, newest-first
            var names = cut.FindAll(".ln-mcard__name");
            Assert.Equal(3, names.Count);
            Assert.Contains("Maritime innovation", names[0].TextContent);
        });
        // the default headline numbers keep the stats band populated
        Assert.Contains("+250", cut.Markup);
    }

    private static PublicArchiveEdition Edition(
        int year, string titleEn, string titleAr,
        int attendees, int sessions, int speakers) =>
        new(Guid.NewGuid(), year, titleEn, titleAr,
            SummaryEn: "Summary", SummaryAr: "ملخص",
            attendees, sessions, speakers, CoverImageRelativePath: null);

    // The page makes exactly one anonymous GET (archive); route every request to the
    // canned envelope, serialising with the web defaults SimfPublicClient reads.
    private sealed class StubArchiveHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public ApiResult<PublicArchive> Archive { get; set; } =
            ApiResult<PublicArchive>.Ok(new PublicArchive(Array.Empty<PublicArchiveEdition>()));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(Archive, Web), Web)),
            });
    }
}
