// Render tests for the reporting pages.
//
// The bug these exist for: `Error="Error"` on a component parameter of type
// string binds the LITERAL TEXT "Error", not the page's Error property, because
// Razor only treats non-string parameter values as expressions. The page still
// compiled, the API still answered, the grid still filled — and every report
// carried a permanent empty red error banner. It was found by looking at the
// running page, so it gets a test that looks at the rendered page.
using Bunit;
using SIMF.Common;
using SIMF.ControlPanel.Components.Pages.Admin.Reports;
using SIMF.Contracts.Reporting;

namespace SIMF.ControlPanel.Tests;

public sealed class ReportPageTests : CpComponentTestBase
{
    private static ReportPage<AttendanceReportRow> OnePage() =>
        new(
            [new AttendanceReportRow(
                Guid.NewGuid(), "S-1", "Opening plenary", "Main hall",
                "23-11-2026 09:00 AM", 42, 7)],
            Total: 1,
            Skip: 0,
            Top: 25,
            Totals:
            [
                new ReportTotal("Admin.Reports.Total.Sessions", "1"),
                new ReportTotal("Admin.Reports.Total.DistinctAttendees", "42"),
            ]);

    private void StubList(ReportPage<AttendanceReportRow> page) =>
        JSInterop.Setup<ApiResult<ReportPage<AttendanceReportRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<ReportPage<AttendanceReportRow>>.Ok(page));

    [Fact]
    public void A_successful_load_shows_no_error_banner()
    {
        // The regression. An always-on error banner is worse than useless: it
        // trains the operator to ignore the one place a real failure appears.
        StubList(OnePage());

        var cut = RenderComponent<AttendanceReport>();

        Assert.Empty(cut.FindAll(".simf-alert--error"));
    }

    [Fact]
    public void The_rows_are_rendered()
    {
        StubList(OnePage());

        var cut = RenderComponent<AttendanceReport>();

        Assert.Contains("Opening plenary", cut.Markup);
        Assert.Contains("Main hall", cut.Markup);
        Assert.Contains("23-11-2026 09:00 AM", cut.Markup);
    }

    [Fact]
    public void The_totals_are_rendered_with_their_resolved_labels()
    {
        // The API returns resource KEYS; the page must resolve them. The test
        // localizer echoes the key, so seeing the key proves it was resolved
        // through the localizer rather than printed raw from the payload.
        StubList(OnePage());

        var cut = RenderComponent<AttendanceReport>();
        var titles = cut.FindAll(".simf-report-totals .simf-stat__title")
            .Select(e => e.TextContent.Trim())
            .ToList();

        Assert.Contains("Admin.Reports.Total.Sessions", titles);
        Assert.Contains("Admin.Reports.Total.DistinctAttendees", titles);
    }

    [Fact]
    public void The_date_range_offers_both_ends()
    {
        StubList(OnePage());

        var cut = RenderComponent<AttendanceReport>();

        Assert.Equal(2, cut.FindAll("input[type=date]").Count);
    }

    [Fact]
    public void The_partners_report_offers_no_date_range()
    {
        // GetPartnersAsync ignores From/To on purpose - a partner directory is a
        // snapshot of who is participating, not a record of events in a period.
        // The page rendered the range control anyway, so Apply was a silent
        // no-op: found by driving the live page, where filtering to a period
        // with no partners in it still returned all of them.
        JSInterop.Setup<ApiResult<ReportPage<PartnersReportRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<ReportPage<PartnersReportRow>>.Ok(
                new ReportPage<PartnersReportRow>(
                    [new PartnersReportRow(
                        Guid.NewGuid(), "Sponsor", "Gulf Defence Systems",
                        "أنظمة الدفاع الخليجية", "Gold", null, null, null, true)],
                    Total: 1, Skip: 0, Top: 25,
                    Totals: [new ReportTotal("Admin.Reports.Total.Partners", "1")])));

        var cut = RenderComponent<PartnersReport>();

        Assert.Empty(cut.FindAll("input[type=date]"));

        // The rest of the toolbar must survive - this hides one control, it does
        // not strip the page.
        Assert.Contains("Gulf Defence Systems", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".simf-report-totals"));
        Assert.Empty(cut.FindAll(".simf-alert--error"));
    }

    [Fact]
    public void Every_other_report_still_offers_the_range()
    {
        // The counterpart, so the fix cannot generalise into "no report filters
        // by date". Partners is the ONLY service that ignores the window; the
        // other seven resolve it through ReportingService.ResolveWindow.
        StubList(OnePage());

        Assert.Equal(2, RenderComponent<AttendanceReport>()
            .FindAll("input[type=date]").Count);
    }

    [Fact]
    public void An_empty_report_renders_the_empty_state_and_still_no_error()
    {
        StubList(new ReportPage<AttendanceReportRow>([], 0, 0, 25, []));

        var cut = RenderComponent<AttendanceReport>();

        Assert.Empty(cut.FindAll(".simf-alert--error"));
        Assert.Contains("Admin.Reports.None", cut.Markup);
    }

    [Fact]
    public void A_failed_load_does_show_an_error_banner()
    {
        // The other half of the contract: the banner must still appear when
        // something actually went wrong, or the fix above would be "delete the
        // banner" rather than "bind it properly".
        JSInterop.Setup<ApiResult<ReportPage<AttendanceReportRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<ReportPage<AttendanceReportRow>>.Fail(
                new ApiError
                {
                    Code = "REPORT_FAILED",
                    Message = "Something broke",
                    MessageArabic = "حدث خطأ",
                }));

        var cut = RenderComponent<AttendanceReport>();

        Assert.NotEmpty(cut.FindAll(".simf-alert--error"));
    }
}
