// Tests: bUnit coverage for the public Website /programme agenda page
// (Programme.razor + Programme.razor.cs). D-628 — the page's C# moved to a
// code-behind partial (Website clean-code, Phase 5) and it had zero component
// coverage before; this pins the three render branches: a populated agenda +
// speakers strip, the empty state, and the API-failure error alert.
//
// The page was re-skinned from the legacy Simf* chrome onto the shared ln-
// marketing kit (LandingShell + LandingPageHero + the ln-agenda band); the data
// flow in the code-behind is unchanged. The structural asserts below guard the
// ln- DOM (ln-pghero hero, ln-agenda rows, ln-fsection chrome) alongside the
// three render branches.
//
// SimfPublicClient is sealed over HttpClient, so the two anonymous reads
// (programme/sessions, speakers) are driven by a routing stub handler returning
// canned ApiResult envelopes serialised with the same web defaults the client reads.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Programme;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class ProgrammePageTests : WebComponentTestBase
{
    private readonly StubPublicHandler _handler = new();

    public ProgrammePageTests()
    {
        // Pin the culture: Pick()'s Arabic-preferred fallback reads
        // CultureInfo.CurrentUICulture directly (not the injected localizer),
        // so the populated-branch asserts on English values would false-fail on
        // an ar-* configured runner. Fixing the culture proves the code, not the
        // environment.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // /programme is anonymous; it injects only SimfPublicClient (+ the
        // localizer from the base). Loose JS covers any shared component that
        // reaches the DOM through JS.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new SimfPublicClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
    }

    [Fact]
    public void Renders_day_sections_and_the_speakers_strip_when_the_agenda_loads()
    {
        _handler.Sessions = ApiResult<PublicSessions>.Ok(new PublicSessions(new[]
        {
            Session("Opening Plenary", "الجلسة الافتتاحية"),
        }));
        _handler.Speakers = ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(new[]
        {
            new PublicSpeakerSummary(Guid.NewGuid(), "Jane Roe", "جين رو",
                "Chief Scientist", null, null, null, null, null, 0),
        }));

        var cut = RenderComponent<Programme>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opening Plenary", cut.Markup);
            Assert.Contains("Jane Roe", cut.Markup);
            Assert.Contains("Chief Scientist", cut.Markup);
            // ln- re-skin: the interior hero, the agenda rows and the speakers strip.
            Assert.Contains("ln-pghero", cut.Markup);
            Assert.Contains("ln-agenda__row", cut.Markup);
            Assert.Contains("ln-agenda__spk", cut.Markup);
            // Event-local (+03:00) window via the shared EventTime helper: the
            // fixture's 09:00–10:30 UTC renders as 12:00 – 13:30 Riyadh. Pins the
            // offset shift + the "HH:mm – HH:mm" format the refactor centralized.
            Assert.Contains("12:00 – 13:30", cut.Markup);
        });
    }

    [Fact]
    public void Renders_the_empty_state_when_the_agenda_has_no_sessions()
    {
        _handler.Sessions = ApiResult<PublicSessions>.Ok(
            new PublicSessions(Array.Empty<PublicSessionListItem>()));

        var cut = RenderComponent<Programme>();

        cut.WaitForAssertion(() =>
        {
            // The pass-through localizer emits each resx key verbatim.
            Assert.Contains("Programme.Empty.Title", cut.Markup);
            Assert.DoesNotContain("Programme.Error", cut.Markup);
            // Empty state renders on the ln- section chrome, not the legacy SimfEmptyState.
            Assert.Contains("ln-fsection", cut.Markup);
        });
    }

    [Fact]
    public void Renders_the_error_alert_when_the_agenda_request_fails()
    {
        // A failed envelope makes the client return null, so the page shows its
        // error state and never fetches the speakers strip.
        _handler.Sessions = ApiResult<PublicSessions>.Fail(new ApiError
        {
            Code = "SERVER_ERROR",
            Message = "The agenda could not be loaded.",
            MessageArabic = "تعذر تحميل الأجندة.",
        });

        var cut = RenderComponent<Programme>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Programme.Error", cut.Markup);
            // Error renders in the ln- message block, not the legacy SimfAlert.
            Assert.Contains("ln-agenda__msg", cut.Markup);
        });
    }

    private static PublicSessionListItem Session(string title, string titleArabic) =>
        new(Guid.NewGuid(), "S-01", title, titleArabic,
            Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
            new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 23, 10, 30, 0, TimeSpan.Zero),
            PrimaryThemeName: null, PrimaryThemeNameArabic: null, PrimaryThemeColor: null);

    // Routes the two anonymous public GETs to their canned envelope by path,
    // serialising with the web defaults SimfPublicClient reads with.
    private sealed class StubPublicHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        public ApiResult<PublicSessions> Sessions { get; set; } =
            ApiResult<PublicSessions>.Ok(new PublicSessions(Array.Empty<PublicSessionListItem>()));

        public ApiResult<PublicSpeakers> Speakers { get; set; } =
            ApiResult<PublicSpeakers>.Ok(new PublicSpeakers(Array.Empty<PublicSpeakerSummary>()));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object envelope = request.RequestUri!.AbsolutePath.Contains("programme/sessions")
                ? Sessions
                : Speakers;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(envelope, Web), Web)),
            });
        }
    }
}
