// D-753 (dynamic forum-date message) — the speaker-availability out-of-range
// toast used to read a hardcoded "23-25 November 2026". It now renders the live
// forum window (SIMF.Common.EventDateRange) into the "{0}" placeholder of
// Admin.SpeakerAvailability.BadDateRange, choosing Arabic vs English from the
// current UI culture's text direction. These tests drive the page with a forum
// window read from the (stubbed) backend, submit an out-of-range window, and
// assert the toast carries the formatted range in both directions.
using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class SpeakerAvailabilityBadDateRangeTests : CpComponentTestBase
{
    private static readonly DateOnly ForumMin = new(2026, 11, 23);
    private static readonly DateOnly ForumMax = new(2026, 11, 25);

    [Fact]
    public void Out_of_range_window_toast_renders_the_english_forum_range()
    {
        var cut = RenderReadyPage();

        SubmitOutOfRangeWindow(cut);

        // EventDateRange collapses the same-month window to "23-25 November 2026".
        var expected = EventDateRange.Format(ForumMin, ForumMax, arabic: false);
        Assert.Equal("23-25 November 2026", expected);
        Assert.Contains($"Dates must be within {expected}.", cut.Markup);
    }

    [Fact]
    public void Out_of_range_window_toast_renders_the_arabic_forum_range_when_rtl()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("ar");
        try
        {
            var cut = RenderReadyPage();

            SubmitOutOfRangeWindow(cut);

            var expected = EventDateRange.Format(ForumMin, ForumMax, arabic: true);
            Assert.Equal("23-25 نوفمبر 2026", expected);
            Assert.Contains($"يجب أن تكون التواريخ ضمن {expected}.", cut.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // Renders the page with the speakers list + forum window stubbed, then
    // selects the single speaker so the add-window form is shown.
    private IRenderedComponent<SpeakerAvailabilityPage> RenderReadyPage()
    {
        // The mock localizer must expose the real "{0}" format so the range
        // actually flows into the rendered toast.
        Services.AddSingleton<IStringLocalizer<Strings>>(new BadDateRangeLocalizer());

        JSInterop.Mode = JSRuntimeMode.Loose;

        var speaker = new AdminSpeakerSummary(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "SPK-1", "Test Speaker", "متحدث اختبار",
            Rank: null, RankArabic: null,
            CountryId: null, CountryNameEn: null, CountryNameAr: null, CountryCode: null,
            DisplayOrder: 0, IsActive: true, HasPhoto: false, CreatedAt: DateTime.UnixEpoch);

        var page = GridPage<AdminSpeakerSummary>.Of(
            new[] { speaker }, total: 1, new GridQuery { Top = 500 });

        JSInterop.Setup<ApiResult<GridPage<AdminSpeakerSummary>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminSpeakerSummary>>.Ok(page));

        JSInterop.Setup<ApiResult<ForumWindowResponse>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<ForumWindowResponse>.Ok(
                new ForumWindowResponse(ForumMin, ForumMax)));

        var cut = RenderComponent<SpeakerAvailabilityPage>();

        // Pick the one speaker so the add-window form renders. The
        // availability-windows GET is left to Loose mode (returns an empty list).
        cut.Find("#spk-pick").Change(speaker.Id.ToString());

        return cut;
    }

    // Enters a window entirely after the forum's last day and clicks Add so the
    // client-side forum-day guard fires and sets the toast.
    private static void SubmitOutOfRangeWindow(IRenderedComponent<SpeakerAvailabilityPage> cut)
    {
        var inputs = cut.FindAll("input.simf-field__input");
        inputs[0].Change("2026-12-01T10:00"); // Start — after the forum max day
        cut.FindAll("input.simf-field__input")[1].Change("2026-12-01T11:00"); // End

        cut.Find(".simf-form__actions button").Click();
    }

    // Mirrors the real resx: returns the "{0}" format for the out-of-range key
    // (culture-appropriate) and the key itself for every other lookup, so the
    // formatted range is what actually reaches the DOM.
    private sealed class BadDateRangeLocalizer : IStringLocalizer<Strings>
    {
        private const string Key = "Admin.SpeakerAvailability.BadDateRange";

        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var format = name == Key
                    ? (CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
                        ? "يجب أن تكون التواريخ ضمن {0}."
                        : "Dates must be within {0}.")
                    : name;
                return new(name, string.Format(CultureInfo.CurrentCulture, format, arguments),
                    resourceNotFound: false);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }
}
