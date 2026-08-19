// The reporting module: POST /api/v1/admin/reports/{slug}/list and /export.
//
// The behaviour worth guarding is the DATE RANGE. From and To are inclusive
// Saudi calendar days, and instants are stored as a zoned value, so the exclusive upper
// bound has to be the start of the day AFTER To. Get that wrong and every report
// silently drops its final day, which is the day people look at first.
//
// Isolation: the suite shares one database, so each test works inside its own
// never-reused date block (see NextBlock).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Reporting;
using SIMF.Domain.AccessControl;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Feedback;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ReportingTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string AttendanceList = "/api/v1/admin/reports/attendance/list";
    private const string AttendanceExport = "/api/v1/admin/reports/attendance/export";
    private const string RegistrationsList = "/api/v1/admin/reports/registrations/list";
    private const string GatesList = "/api/v1/admin/reports/gates/list";
    private const string GatesExport = "/api/v1/admin/reports/gates/export";
    private const string RatingsList = "/api/v1/admin/reports/ratings/list";
    private const string EngagementList = "/api/v1/admin/reports/engagement/list";
    private const string MeetingsList = "/api/v1/admin/reports/meetings/list";

    /// <summary>The first bytes of any XLSX: it is a ZIP container.</summary>
    private static readonly byte[] ZipMagic = [0x50, 0x4B];

    private static readonly TimeSpan Ast = TimeSpan.FromHours(3);
    private static int _blockCounter = -1;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ReportingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- The inclusive date range -------------------------------------------

    [Fact]
    public async Task The_last_day_of_the_range_is_included()
    {
        // The regression this file exists for. A session at midday on the To day
        // must appear; an exclusive upper bound of "To 00:00" would drop it.
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 12), "LASTDAY");

        var page = await ListAttendanceAsync(token, from: day, to: day);

        Assert.Contains(page.Rows, r => r.Code == "LASTDAY");
    }

    [Fact]
    public async Task The_first_day_of_the_range_is_included()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 0, 5), "FIRSTDAY");

        var page = await ListAttendanceAsync(token, from: day, to: day);

        Assert.Contains(page.Rows, r => r.Code == "FIRSTDAY");
    }

    [Fact]
    public async Task A_session_late_on_the_last_saudi_evening_is_included()
    {
        // 23:30 Riyadh on the To day is 20:30 a zoned value the same day, but 01:00 Riyadh
        // the NEXT day is 22:00 a zoned value on the To day. Only the first belongs.
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 23, 30), "LATE");
        await SeedSessionAsync(hallId, SaudiAt(day.AddDays(1), 1, 0), "NEXTDAY");

        var page = await ListAttendanceAsync(token, from: day, to: day);

        Assert.Contains(page.Rows, r => r.Code == "LATE");
        Assert.DoesNotContain(page.Rows, r => r.Code == "NEXTDAY");
    }

    [Fact]
    public async Task A_session_before_the_range_is_excluded()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day.AddDays(-1), 12), "BEFORE");
        await SeedSessionAsync(hallId, SaudiAt(day, 12), "INSIDE");

        var page = await ListAttendanceAsync(token, from: day, to: day);

        Assert.Contains(page.Rows, r => r.Code == "INSIDE");
        Assert.DoesNotContain(page.Rows, r => r.Code == "BEFORE");
    }

    [Fact]
    public async Task An_open_ended_range_returns_everything_from_the_start_date()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day.AddDays(3), 12), "LATER");

        var page = await ListAttendanceAsync(token, from: day, to: null);

        Assert.Contains(page.Rows, r => r.Code == "LATER");
    }

    // -- The figures ---------------------------------------------------------

    [Fact]
    public async Task Attendance_counts_distinct_people_not_arrivals()
    {
        // Someone who steps out and comes back is one attendee, not two.
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, SaudiAt(day, 10), "DISTINCT");

        var userId = Guid.NewGuid();
        await SeedArrivalAsync(sessionId, hallId, userId, SaudiAt(day, 10), left: true);
        await SeedArrivalAsync(sessionId, hallId, userId, SaudiAt(day, 11), left: true);
        await SeedArrivalAsync(sessionId, hallId, Guid.NewGuid(), SaudiAt(day, 11), left: true);

        var page = await ListAttendanceAsync(token, from: day, to: day);
        var row = page.Rows.Single(r => r.Code == "DISTINCT");

        Assert.Equal(2, row.Attendees);
    }

    [Fact]
    public async Task Inside_now_counts_only_arrivals_that_have_not_left()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, SaudiAt(day, 10), "INSIDE-NOW");

        await SeedArrivalAsync(sessionId, hallId, Guid.NewGuid(), SaudiAt(day, 10), left: true);
        await SeedArrivalAsync(sessionId, hallId, Guid.NewGuid(), SaudiAt(day, 10), left: false);

        var page = await ListAttendanceAsync(token, from: day, to: day);
        var row = page.Rows.Single(r => r.Code == "INSIDE-NOW");

        Assert.Equal(2, row.Attendees);
        Assert.Equal(1, row.LiveNow);
    }

    [Fact]
    public async Task Totals_describe_the_whole_filtered_set_not_the_visible_page()
    {
        // Page size 1 over three sessions: the header total must still say 3.
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 9), "T1");
        await SeedSessionAsync(hallId, SaudiAt(day, 10), "T2");
        await SeedSessionAsync(hallId, SaudiAt(day, 11), "T3");

        var page = await ListAttendanceAsync(
            token, from: day, to: day, top: 1);

        Assert.Single(page.Rows);
        Assert.Equal(3, page.Total);
        Assert.Contains(page.Totals, t =>
            t.LabelKey == "Admin.Reports.Total.Sessions" && t.Value == "3");
    }

    [Fact]
    public async Task Dates_are_rendered_in_saudi_local_time_never_utc()
    {
        // 01:00 Riyadh is 22:00 a zoned value the previous day. The rendered string must
        // show the Saudi wall clock, or the report contradicts every other
        // surface (D-770).
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 1, 0), "TZ");

        var page = await ListAttendanceAsync(token, from: day, to: day);
        var row = page.Rows.Single(r => r.Code == "TZ");

        Assert.Equal(day.ToString("dd-MM-yyyy"), row.StartDisplay[..10]);
        Assert.Contains("01:00 AM", row.StartDisplay);
    }

    [Fact]
    public async Task Registrations_report_returns_the_accounts_created_in_the_period()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var email = await SeedVisitorRegisteredAtAsync(SaudiAt(day, 12));

        var page = await ListAsync<RegistrationReportRow>(
            token, RegistrationsList, day, day);

        Assert.Contains(page.Rows, r => r.Email == email);
        Assert.All(page.Rows, r => Assert.NotEqual(string.Empty, r.RegisteredDisplay));
    }

    [Fact]
    public async Task Gate_report_returns_allowed_and_denied_scans_with_the_reason()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var gateId = await SeedGateAsync();
        await SeedScanAsync(gateId, SaudiAt(day, 9), ScanOutcome.Allowed);
        await SeedScanAsync(gateId, SaudiAt(day, 10), ScanOutcome.Denied);

        var page = await ListAsync<GateActivityReportRow>(token, GatesList, day, day);

        Assert.Equal(2, page.Total);
        Assert.Contains(page.Rows, r => r.Outcome == "Allowed" && r.DenialReason is null);
        Assert.Contains(page.Rows, r => r.Outcome == "Denied" && r.DenialReason is not null);
        Assert.Contains(page.Totals, t => t.LabelKey == "Admin.Reports.Total.Denied" && t.Value == "1");
    }

    // -- Meetings ordering ----------------------------------------------------

    // The default arm used to order on the RENDERED "requested" cell, which is a
    // day-first 12-hour string: "31-08-2042" sorts above "01-09-2042" ordinally,
    // so the report presented the older request as the newest. The "requested"
    // column is not sortable in the CP, so every page load took this arm.
    [Fact]
    public async Task Meetings_default_order_is_newest_first_across_a_month_boundary()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        var older = new DateOnly(2042, 8, 31);
        var newer = new DateOnly(2042, 9, 1);

        // Seeded oldest-last so an unordered read cannot pass by accident.
        await SeedSpeakerMeetingRequestAsync(speakerId, "AUGUST-31", SaudiAt(older, 9));
        await SeedSpeakerMeetingRequestAsync(speakerId, "SEPTEMBER-01", SaudiAt(newer, 10));

        var page = await ListAsync<MeetingsReportRow>(
            token, MeetingsList, new DateOnly(2042, 8, 1), new DateOnly(2042, 9, 30), top: 200);

        var subjects = page.Rows.Select(r => r.Subject).ToList();
        var septemberAt = subjects.IndexOf("SEPTEMBER-01");
        var augustAt = subjects.IndexOf("AUGUST-31");
        Assert.True(septemberAt >= 0 && augustAt >= 0, "both seeded requests must be in the page");
        Assert.True(
            septemberAt < augustAt,
            "the 1 September request is newer and must sort above the 31 August one");
    }

    // Same-day the formatted string was equally wrong: "11:00 AM" > "01:00 PM"
    // ordinally, so a morning request outranked an afternoon one.
    [Fact]
    public async Task Meetings_default_order_puts_the_afternoon_request_above_the_morning_one()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        var day = new DateOnly(2042, 10, 5);

        await SeedSpeakerMeetingRequestAsync(speakerId, "MORNING-11AM", SaudiAt(day, 11));
        await SeedSpeakerMeetingRequestAsync(speakerId, "AFTERNOON-1PM", SaudiAt(day, 13));

        var page = await ListAsync<MeetingsReportRow>(token, MeetingsList, day, day, top: 200);

        var subjects = page.Rows.Select(r => r.Subject).ToList();
        Assert.True(
            subjects.IndexOf("AFTERNOON-1PM") < subjects.IndexOf("MORNING-11AM"),
            "the 1pm request is newer and must sort above the 11am one");
    }

    // -- Export --------------------------------------------------------------

    [Fact]
    public async Task Attendance_export_returns_an_xlsx_workbook()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(day, 12), "XLSX");

        var response = await PostAuthAsync(AttendanceExport, Range(day, day), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0, "the workbook was empty");
        Assert.Equal(ZipMagic, bytes[..2]);
    }

    [Fact]
    public async Task Export_carries_an_attachment_filename()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();

        var response = await PostAuthAsync(GatesExport, Range(day, day), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition?.ToString()
            ?? string.Join(";", response.Headers.TryGetValues("Content-Disposition", out var v)
                ? v : []);
        Assert.Contains("attachment", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gate-activity", disposition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_empty_report_still_exports_a_valid_workbook()
    {
        // A period with no records must produce a headers-only sheet, not a
        // zero-byte file the operator's Excel refuses to open.
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();

        var response = await PostAuthAsync(AttendanceExport, Range(day, day), token);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length > 0);
        Assert.Equal(ZipMagic, bytes[..2]);
    }

    // -- Authorisation -------------------------------------------------------

    [Fact]
    public async Task Anonymous_caller_is_rejected_on_every_report()
    {
        foreach (var route in new[] { AttendanceList, RegistrationsList, GatesList })
        {
            var response = await _client.PostAsJsonAsync(route, new ReportQuery());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Anonymous_caller_is_rejected_on_export()
    {
        var response = await _client.PostAsJsonAsync(AttendanceExport, new ReportQuery());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            AttendanceList, new ReportQuery(), tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            AttendanceExport, new ReportQuery(), tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- The five later reports ----------------------------------------------

    [Theory]
    [InlineData("sessions")]
    [InlineData("ratings")]
    [InlineData("partners")]
    [InlineData("meetings")]
    [InlineData("engagement")]
    public async Task Every_report_lists_without_error(string slug)
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            $"/api/v1/admin/reports/{slug}/list", new ReportQuery(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("ratings")]
    [InlineData("partners")]
    [InlineData("meetings")]
    [InlineData("engagement")]
    public async Task Every_report_exports_a_valid_workbook(string slug)
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            $"/api/v1/admin/reports/{slug}/export", new ReportQuery(), token);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length > 0, $"the {slug} workbook was empty");
        Assert.Equal(ZipMagic, bytes[..2]);
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("ratings")]
    [InlineData("partners")]
    [InlineData("meetings")]
    [InlineData("engagement")]
    public async Task Every_report_refuses_an_anonymous_caller(string slug)
    {
        var list = await _client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{slug}/list", new ReportQuery());
        var export = await _client.PostAsJsonAsync(
            $"/api/v1/admin/reports/{slug}/export", new ReportQuery());

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, export.StatusCode);
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("ratings")]
    [InlineData("partners")]
    [InlineData("meetings")]
    [InlineData("engagement")]
    public async Task Every_report_refuses_a_non_admin_caller(string slug)
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            $"/api/v1/admin/reports/{slug}/list", new ReportQuery(), tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_sessions_report_counts_attendees_and_questions_per_session()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, SaudiAt(day, 10), "SESRPT");

        await SeedArrivalAsync(sessionId, hallId, Guid.NewGuid(), SaudiAt(day, 10), left: true);
        await SeedArrivalAsync(sessionId, hallId, Guid.NewGuid(), SaudiAt(day, 10), left: false);

        var page = await ListAsync<SessionsReportRow>(
            token, "/api/v1/admin/reports/sessions/list", day, day);
        var row = page.Rows.Single(r => r.Code == "SESRPT");

        Assert.Equal(2, row.Attendees);
        Assert.Equal(0, row.Questions);
        // No ratings seeded: the score must be blank, not a misleading "0.0".
        Assert.Equal(string.Empty, row.AverageRating);
    }

    [Fact]
    public async Task The_partners_report_flattens_the_three_partner_kinds()
    {
        // The report exists so an organiser reads one contact list, not three.
        var token = await CreateAdministratorAndSignInAsync();

        var page = await ListAsync<PartnersReportRow>(
            token, "/api/v1/admin/reports/partners/list", from: null, to: null, top: 200);

        Assert.All(page.Rows, r =>
            Assert.Contains(r.Kind, new[] { "Exhibitor", "Sponsor", "Booth" }));
        Assert.Contains(page.Totals, t => t.LabelKey == "Admin.Reports.Total.Exhibitors");
        Assert.Contains(page.Totals, t => t.LabelKey == "Admin.Reports.Total.Sponsors");
        Assert.Contains(page.Totals, t => t.LabelKey == "Admin.Reports.Total.Booths");
    }

    [Fact]
    public async Task The_partners_report_ignores_the_date_range()
    {
        // A partner directory is a snapshot of who is participating, not a log
        // of events in a period, so a range must not empty it.
        var token = await CreateAdministratorAndSignInAsync();

        var unbounded = await ListAsync<PartnersReportRow>(
            token, "/api/v1/admin/reports/partners/list", from: null, to: null, top: 200);
        var ranged = await ListAsync<PartnersReportRow>(
            token, "/api/v1/admin/reports/partners/list",
            from: new DateOnly(2031, 1, 1), to: new DateOnly(2031, 1, 2), top: 200);

        Assert.Equal(unbounded.Total, ranged.Total);
    }

    // -- Paging --------------------------------------------------------------

    [Fact]
    public async Task A_page_size_beyond_the_cap_is_clamped()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();

        var page = await ListAttendanceAsync(token, from: day, to: day, top: 10_000);

        Assert.True(page.Top <= 200, $"page size {page.Top} exceeded the cap");
    }

    // -- The date column sorts in the direction its arrow claims -------------
    //
    // Every report grid's date column is Sortable, so a click sends that
    // column's own Key with SortDescending=false on the first press and true on
    // the second (SimfDataGrid.ToggleSortAsync). Four of the sort switches had
    // no arm for that key, so the click fell through to their default — and
    // that default deliberately reads `descending` INVERTED, because the
    // no-sort case has to come back newest-first. The two wrongs did not cancel:
    // the grid drew an ascending arrow (and rendered aria-sort="ascending",
    // which a screen reader announces) over newest-first rows, and a descending
    // arrow over oldest-first ones. Each test below seeds an early and a late
    // record inside its own date block and asserts BOTH directions, so removing
    // the named arm fails rather than merely reordering.

    [Fact]
    public async Task Registrations_sort_on_registered_follows_the_arrow()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var early = await SeedVisitorRegisteredAtAsync(SaudiAt(day, 6));
        var late = await SeedVisitorRegisteredAtAsync(SaudiAt(day, 18));

        var ascending = await SortedAsync<RegistrationReportRow>(
            token, RegistrationsList, day, "registered", descending: false);
        var descending = await SortedAsync<RegistrationReportRow>(
            token, RegistrationsList, day, "registered", descending: true);

        AssertOrder(
            ascending.Rows.Select(r => r.Email).ToList(),
            descending.Rows.Select(r => r.Email).ToList(),
            early, late);
    }

    [Fact]
    public async Task Gate_activity_sorts_on_scanned_following_the_arrow()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var gateId = await SeedGateAsync();
        // Seeded late-first, so a switch that ignored the key and fell back to
        // insertion order could not pass by accident.
        await SeedScanAsync(gateId, SaudiAt(day, 18), ScanOutcome.Allowed, "Late Scan");
        await SeedScanAsync(gateId, SaudiAt(day, 6), ScanOutcome.Allowed, "Early Scan");

        var ascending = await SortedAsync<GateActivityReportRow>(
            token, GatesList, day, "scanned", descending: false);
        var descending = await SortedAsync<GateActivityReportRow>(
            token, GatesList, day, "scanned", descending: true);

        AssertOrder(
            ascending.Rows.Select(r => r.VisitorName ?? string.Empty).ToList(),
            descending.Rows.Select(r => r.VisitorName ?? string.Empty).ToList(),
            "Early Scan", "Late Scan");
    }

    [Fact]
    public async Task Ratings_sort_on_submitted_following_the_arrow()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var typeId = await SeedRatingTypeAsync();
        var early = await SeedRatingResponseAsync(typeId, SaudiAt(day, 6));
        var late = await SeedRatingResponseAsync(typeId, SaudiAt(day, 18));

        var ascending = await SortedAsync<RatingsReportRow>(
            token, RatingsList, day, "submitted", descending: false);
        var descending = await SortedAsync<RatingsReportRow>(
            token, RatingsList, day, "submitted", descending: true);

        AssertOrder(
            ascending.Rows.Select(r => r.Comment ?? string.Empty).ToList(),
            descending.Rows.Select(r => r.Comment ?? string.Empty).ToList(),
            early, late);
    }

    [Fact]
    public async Task Engagement_sorts_on_asked_following_the_arrow()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();
        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, SaudiAt(day, 9), "ENGSORT");
        var early = await SeedQuestionAsync(sessionId, SaudiAt(day, 6));
        var late = await SeedQuestionAsync(sessionId, SaudiAt(day, 18));

        var ascending = await SortedAsync<EngagementReportRow>(
            token, EngagementList, day, "asked", descending: false);
        var descending = await SortedAsync<EngagementReportRow>(
            token, EngagementList, day, "asked", descending: true);

        AssertOrder(
            ascending.Rows.Select(r => r.QuestionText).ToList(),
            descending.Rows.Select(r => r.QuestionText).ToList(),
            early, late);
    }

    /// <summary>Both directions, in one place: ascending must lead with the
    /// earlier record and descending must lead with the later one. Asserting
    /// only one direction would pass against a switch that ignores the key
    /// entirely and happens to default the right way.</summary>
    private static void AssertOrder(
        List<string> ascending,
        List<string> descending,
        string early,
        string late)
    {
        Assert.Contains(early, ascending);
        Assert.Contains(late, ascending);
        Assert.True(
            ascending.IndexOf(early) < ascending.IndexOf(late),
            "The ascending arrow must put the EARLIER record first. Getting the "
            + "later one means the sort key reached no arm and fell through to a "
            + "default that reads the direction inverted.");
        Assert.True(
            descending.IndexOf(late) < descending.IndexOf(early),
            "The descending arrow must put the LATER record first.");
    }

    // -- Helpers -------------------------------------------------------------

    /// <summary>A fresh date no other test in this class has used, so records
    /// seeded elsewhere cannot fall inside this test's period.</summary>
    private static DateOnly NextBlock() =>
        new DateOnly(2032, 1, 1).AddDays(Interlocked.Increment(ref _blockCounter) * 7);

    private static DateTime SaudiAt(DateOnly day, int hour, int minute = 0) =>
        day.ToDateTime(new TimeOnly(hour, minute));

    private static ReportQuery Range(DateOnly? from, DateOnly? to, int top = 25) =>
        new() { From = from, To = to, Grid = new GridQuery { Top = top } };

    /// <summary>One report page for a single date block, sorted the way the grid
    /// asks for it: the column's own Key plus the direction its arrow shows.</summary>
    private async Task<ReportPage<TRow>> SortedAsync<TRow>(
        string token, string route, DateOnly day, string sort, bool descending)
    {
        var query = new ReportQuery
        {
            From = day,
            To = day,
            Grid = new GridQuery { Top = 200, Sort = sort, SortDescending = descending },
        };
        var response = await PostAuthAsync(route, query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<ReportPage<TRow>>>())!.Data!;
    }

    private async Task<ReportPage<AttendanceReportRow>> ListAttendanceAsync(
        string token, DateOnly? from, DateOnly? to, int top = 25) =>
        await ListAsync<AttendanceReportRow>(token, AttendanceList, from, to, top);

    private async Task<ReportPage<TRow>> ListAsync<TRow>(
        string token, string route, DateOnly? from, DateOnly? to, int top = 25)
    {
        var response = await PostAuthAsync(route, Range(from, to, top), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<ReportPage<TRow>>>())!.Data!;
    }

    private async Task<HttpResponseMessage> PostAuthAsync(
        string url, object body, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "RS-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Report Speaker",
            NameArabic = "متحدث التقارير",
            AllowsMeetingRequests = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task SeedSpeakerMeetingRequestAsync(
        Guid speakerId, string subject, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Report Requester",
            Subject = subject,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "RH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Report Hall",
            NameArabic = "قاعة التقارير",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<Guid> SeedSessionAsync(Guid hallId, DateTime startUtc, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = "Report Session",
            TitleArabic = "جلسة التقارير",
            HallId = hallId,
            Start = startUtc,
            End = startUtc.AddHours(1),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task SeedArrivalAsync(
        Guid sessionId, Guid hallId, Guid userId, DateTime enterUtc, bool left)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var attendeeProfileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        db.HallAttendances.Add(new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserProfileId = attendeeProfileId,
            Method = AttendanceMethod.QrScan,
            Enter = enterUtc,
            Leave = left ? enterUtc.AddMinutes(30) : null,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedGateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = "RG-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Report Gate",
            NameArabic = "بوابة التقارير",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Gates.Add(gate);
        await db.SaveChangesAsync();
        return gate.Id;
    }

    /// <param name="displayName">Names the row so a sort assertion can identify
    /// it; the existing range tests do not care and keep the shared default.</param>
    private async Task SeedScanAsync(
        Guid gateId, DateTime scannedAtUtc, ScanOutcome outcome, string? displayName = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.GateScans.Add(new GateScan
        {
            GateId = gateId,
            UserProfileId = null,
            ScannedByUserId = Guid.NewGuid(),
            ScannedAt = scannedAtUtc,
            Direction = ScanDirection.CheckIn,
            Outcome = outcome,
            DenialReasonCode = outcome == ScanOutcome.Denied
                ? DenialReasonCode.HolderNotApproved
                : null,
            ScannedDisplayName = displayName ?? "Report Visitor",
            ScannedProfileTypeName = "Normal",
            QrIdAtScan = "QR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Source = ScanSource.Simulator,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedRatingTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new RatingType
        {
            Id = Guid.NewGuid(),
            Code = "RT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name = "Report Rating",
            NameArabic = "تقييم التقارير",
            Scope = RatingScope.Global,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.RatingTypes.Add(type);
        await db.SaveChangesAsync();
        return type.Id;
    }

    /// <summary>Returns the comment, which is what the report row carries and so
    /// what a sort assertion can identify the row by.</summary>
    private async Task<string> SeedRatingResponseAsync(Guid typeId, DateTime createdAtUtc)
    {
        var comment = "rating-" + Guid.NewGuid().ToString("N")[..10];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.RatingResponses.Add(new RatingResponse
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RatingTypeId = typeId,
            TargetId = Guid.Empty,
            OverallStars = 4,
            Comment = comment,
            IsActive = true,
            CreatedAt = createdAtUtc,
        });
        await db.SaveChangesAsync();
        return comment;
    }

    /// <summary>Returns the question text, which identifies the row in the
    /// engagement report.</summary>
    private async Task<string> SeedQuestionAsync(Guid sessionId, DateTime createdAtUtc)
    {
        var text = "question-" + Guid.NewGuid().ToString("N")[..10];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionQuestions.Add(new SessionQuestion
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SubmittedByUserId = Guid.NewGuid(),
            QuestionText = text,
            CreatedAt = createdAtUtc,
        });
        await db.SaveChangesAsync();
        return text;
    }

    private async Task<string> SeedVisitorRegisteredAtAsync(DateTime createdAtUtc)
    {
        var email = $"report-reg-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Report Registrant",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var stored = await identity.Users.SingleAsync(u => u.Id == user.Id);
        stored.CreatedAt = createdAtUtc;
        await identity.SaveChangesAsync();
        return email;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"report-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Report Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
