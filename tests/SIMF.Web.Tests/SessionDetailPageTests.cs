// Tests: bUnit coverage for the public Website /sessions/{id} page
// (SessionDetail.razor + SessionDetail.razor.cs — Figma 5991-85840). Pins the
// render branches: a fully-populated session (all seven sections — hero, at-a-
// glance, key themes, speakers, related strip, downloads, outcomes), the
// not-found state (unknown id), and graceful omission of the data sections that
// are empty. Live prod data has no themes/speakers/outcomes/downloads yet, so
// this test is where those sections are proven to render.
//
// The page wraps its sections in the shared LandingShell (whose <HeadContent>
// uses the @Assets fingerprint helper), so an empty ResourceAssetCollection is
// registered. Two anonymous reads (GET programme/sessions/{id} + the related
// list) are driven by a routing stub handler returning canned ApiResult
// envelopes serialised with the web defaults the client reads.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class SessionDetailPageTests : WebComponentTestBase
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly StubPublicHandler _handler = new();

    public SessionDetailPageTests()
    {
        // Pin the culture: the page's Pick() helpers read CurrentUICulture, so the
        // English assertions below would false-fail on an ar-* runner.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
        Services.AddSingleton(new SimfPublicClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
    }

    [Fact]
    public void Renders_all_sections_when_the_session_loads()
    {
        _handler.Detail = ApiResult<PublicSessionDetail>.Ok(FullSession());
        _handler.List = ApiResult<PublicSessions>.Ok(new PublicSessions(new[]
        {
            Listed(Guid.NewGuid(), "Panel Session", "جلسة نقاش"),
            Listed(SessionId, "This Session", "هذه الجلسة"), // filtered out (same id)
        }));

        var cut = RenderComponent<SessionDetail>(p => p.Add(x => x.Id, SessionId));

        cut.WaitForAssertion(() =>
        {
            // Hero
            Assert.Contains("Securing Maritime Trade Routes", cut.Markup);
            Assert.Contains("SessionDetail.Breadcrumb.Current", cut.Markup);
            // At-a-glance (language row present because Language is set)
            Assert.Contains("SessionDetail.Glance.Language", cut.Markup);
            Assert.Contains("English &amp; Arabic", cut.Markup);
            // Key themes (with descriptions)
            Assert.Contains("Maritime Security", cut.Markup);
            Assert.Contains("Layered defence of ports", cut.Markup);
            // Speakers
            Assert.Contains("Dr. Sarah Al-Otaibi", cut.Markup);
            // Outcomes
            Assert.Contains("A unified threat picture", cut.Markup);
            // Downloads
            Assert.Contains("agenda.pdf", cut.Markup);
        });

        Assert.Single(cut.FindAll(".ln-tcard"));                 // one theme card
        Assert.Single(cut.FindAll(".ln-spkcard"));               // one speaker card
        Assert.Single(cut.FindAll(".ln-outcomes__item"));        // one outcome
        Assert.Single(cut.FindAll(".ln-docrow"));                // one download
        Assert.Single(cut.FindAll(".ln-rcard"));                 // one related (self filtered out)
        Assert.Empty(cut.FindAll(".ln-sessmissing"));            // NOT the not-found state

        // The download link targets the same-origin proxy for THIS session.
        var link = cut.Find(".ln-docrow__link");
        Assert.Contains($"/content/sessions/{SessionId}/downloads/", link.GetAttribute("href"));
    }

    [Fact]
    public void Renders_the_not_found_state_for_an_unknown_id()
    {
        _handler.Detail = ApiResult<PublicSessionDetail>.Fail(new ApiError
        {
            Code = "SESSION_NOT_FOUND",
            Message = "The session was not found.",
            MessageArabic = "لم يتم العثور على الجلسة.",
        });

        var cut = RenderComponent<SessionDetail>(p => p.Add(x => x.Id, SessionId));

        cut.WaitForAssertion(() => Assert.Contains("SessionDetail.NotFound.Title", cut.Markup));
        Assert.Empty(cut.FindAll(".ln-sesshero"));
        Assert.Single(cut.FindAll(".ln-sessmissing"));
    }

    [Fact]
    public void Omits_the_data_sections_that_are_empty()
    {
        // A minimal session: no themes/speakers/outcomes/downloads/language. The
        // hero + at-a-glance still render; the optional sections are absent.
        _handler.Detail = ApiResult<PublicSessionDetail>.Ok(MinimalSession());
        _handler.List = ApiResult<PublicSessions>.Ok(
            new PublicSessions(Array.Empty<PublicSessionListItem>()));

        var cut = RenderComponent<SessionDetail>(p => p.Add(x => x.Id, SessionId));

        cut.WaitForAssertion(() => Assert.Contains("Opening Session", cut.Markup));
        Assert.Single(cut.FindAll(".ln-sesshero"));              // hero always renders
        Assert.NotEmpty(cut.FindAll(".ln-glance__row"));         // at-a-glance renders
        Assert.Empty(cut.FindAll(".ln-tcard"));                  // no themes
        Assert.Empty(cut.FindAll(".ln-spkcard"));                // no speakers
        Assert.Empty(cut.FindAll(".ln-outcomes__item"));         // no outcomes
        Assert.Empty(cut.FindAll(".ln-docrow"));                 // no downloads
        Assert.Empty(cut.FindAll(".ln-rcard"));                  // no related
        // The language row is omitted when Language is null.
        Assert.DoesNotContain("SessionDetail.Glance.Language", cut.Markup);
    }

    private static PublicSessionDetail FullSession() =>
        new(SessionId, "S-01",
            "Securing Maritime Trade Routes", "تأمين طرق التجارة البحرية",
            "A high-level dialogue on protecting global trade arteries.",
            "حوار رفيع المستوى.",
            Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
            new DateTime(2026, 11, 20, 6, 0, 0),
            new DateTime(2026, 11, 20, 7, 30, 0),
            new[]
            {
                new PublicSessionTheme(Guid.NewGuid(), "Maritime Security", "الأمن البحري",
                    "#244a77", "Layered defence of ports and vessels.", "دفاع متعدد الطبقات."),
            },
            new[]
            {
                new PublicSessionSpeaker(Guid.NewGuid(), "Dr. Sarah Al-Otaibi", "د. سارة العتيبي",
                    "Chief Scientist", 0, SessionSpeakerRole.Speaker,
                    null, "Saudi Arabia", "المملكة العربية السعودية", null, false),
            },
            new PublicSessionSeatSummary(400, 120, 280),
            CategoryName: "Energy & Security", CategoryNameArabic: "الطاقة والأمن",
            Outcomes: new[] { new PublicSessionOutcome("A unified threat picture.", "صورة موحّدة للتهديدات.") },
            Language: "English & Arabic", LanguageArabic: "الإنجليزية والعربية",
            Downloads: new[] { new PublicSessionDownload(Guid.NewGuid(), "agenda.pdf", "application/pdf", 204_800) });

    private static PublicSessionDetail MinimalSession() =>
        new(SessionId, "S-00", "Opening Session", "الجلسة الافتتاحية",
            null, null,
            Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
            new DateTime(2026, 11, 20, 6, 0, 0),
            new DateTime(2026, 11, 20, 7, 0, 0),
            Array.Empty<PublicSessionTheme>(),
            Array.Empty<PublicSessionSpeaker>(),
            new PublicSessionSeatSummary(500, 0, 500));

    private static PublicSessionListItem Listed(Guid id, string title, string titleArabic) =>
        new(id, "R-01", title, titleArabic,
            Guid.NewGuid(), "Hall B", "القاعة ب",
            new DateTime(2026, 11, 21, 6, 0, 0),
            new DateTime(2026, 11, 21, 7, 0, 0),
            PrimaryThemeName: null, PrimaryThemeNameArabic: null, PrimaryThemeColor: null);

    // Routes the session-detail GET (path ends with the session id) vs the agenda
    // list GET to their canned envelope, serialised with the client's web defaults.
    private sealed class StubPublicHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public ApiResult<PublicSessionDetail> Detail { get; set; } =
            ApiResult<PublicSessionDetail>.Fail(new ApiError { Code = "X", Message = "x", MessageArabic = "x" });

        public ApiResult<PublicSessions> List { get; set; } =
            ApiResult<PublicSessions>.Ok(new PublicSessions(Array.Empty<PublicSessionListItem>()));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            object envelope = path.EndsWith(SessionId.ToString()) ? Detail : List;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(envelope, Web), Web)),
            });
        }
    }
}
