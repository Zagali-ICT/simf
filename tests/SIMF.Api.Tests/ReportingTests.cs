// The reporting module: POST /api/v1/admin/reports/{slug}/list and /export.
//
// The behaviour worth guarding is the DATE RANGE. From and To are inclusive
// Saudi calendar days, and instants are stored as UTC, so the exclusive upper
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
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ReportingTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string AttendanceList = "/api/v1/admin/reports/attendance/list";
    private const string AttendanceExport = "/api/v1/admin/reports/attendance/export";
    private const string RegistrationsList = "/api/v1/admin/reports/registrations/list";
    private const string GatesList = "/api/v1/admin/reports/gates/list";
    private const string GatesExport = "/api/v1/admin/reports/gates/export";

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
        // 23:30 Riyadh on the To day is 20:30 UTC the same day, but 01:00 Riyadh
        // the NEXT day is 22:00 UTC on the To day. Only the first belongs.
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
        // 01:00 Riyadh is 22:00 UTC the previous day. The rendered string must
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

    // -- Paging --------------------------------------------------------------

    [Fact]
    public async Task A_page_size_beyond_the_cap_is_clamped()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var day = NextBlock();

        var page = await ListAttendanceAsync(token, from: day, to: day, top: 10_000);

        Assert.True(page.Top <= 200, $"page size {page.Top} exceeded the cap");
    }

    // -- Helpers -------------------------------------------------------------

    /// <summary>A fresh date no other test in this class has used, so records
    /// seeded elsewhere cannot fall inside this test's period.</summary>
    private static DateOnly NextBlock() =>
        new DateOnly(2032, 1, 1).AddDays(Interlocked.Increment(ref _blockCounter) * 7);

    private static DateTimeOffset SaudiAt(DateOnly day, int hour, int minute = 0) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), Ast).ToUniversalTime();

    private static ReportQuery Range(DateOnly? from, DateOnly? to, int top = 25) =>
        new() { From = from, To = to, Grid = new GridQuery { Top = top } };

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
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<Guid> SeedSessionAsync(Guid hallId, DateTimeOffset startUtc, string code)
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
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task SeedArrivalAsync(
        Guid sessionId, Guid hallId, Guid userId, DateTimeOffset enterUtc, bool left)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.HallAttendances.Add(new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserId = userId,
            Method = AttendanceMethod.QrScan,
            Enter = enterUtc,
            Leave = left ? enterUtc.AddMinutes(30) : null,
            CreatedAt = DateTimeOffset.UtcNow,
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
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Gates.Add(gate);
        await db.SaveChangesAsync();
        return gate.Id;
    }

    private async Task SeedScanAsync(Guid gateId, DateTimeOffset scannedAtUtc, ScanOutcome outcome)
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
            ScannedDisplayName = "Report Visitor",
            ScannedProfileTypeName = "Normal",
            QrIdAtScan = "QR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Source = ScanSource.Simulator,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> SeedVisitorRegisteredAtAsync(DateTimeOffset createdAtUtc)
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

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }
}
