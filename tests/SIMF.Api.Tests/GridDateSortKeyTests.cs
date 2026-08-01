// Pins the sort keys the Control Panel's date columns actually send.
//
// THE BUG THIS EXISTS TO CATCH (found 2026-08-01 while writing the admin manual):
// D-770 renamed the persisted date columns, and four service-side sort switches
// were left matching the OLD key while their grids kept sending the column's own
// Key. Nothing failed loudly, because every one of those switches ends in a
// catch-all `_ =>` that silently returns a default order. The result was four
// admin grids whose date column looked sortable and was not:
//
//   /admin/attendance     "start"      vs "startutc"  -> never sorted descending
//   /admin/sessions       "start"/"end" vs "endutc"   -> End sorted the grid by START
//   /admin/banners        "start"/"end" vs "startutc" -> neither column sorted at all
//   /admin/operation-log  "timestamp"  vs "timestamputc" -> never sorted oldest-first
//
// A silent fall-through is the worst shape for this: the arrow flips, the rows do
// not move, and an operator concludes the data is wrong rather than the sort. The
// audit log mattered most, because tracing an incident forward from its start is
// exactly the oldest-first view that was unreachable.
//
// Each test below asserts the DESCENDING (or ascending, where the default is
// descending) direction actually changes the order, so re-breaking the key
// reintroduces a failure instead of a shrug.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Cms;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class GridDateSortKeyTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GridDateSortKeyTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sessions_sort_on_start_honours_the_descending_direction()
    {
        var token = await AuthFlow.SignInControlPanelAsync(
            _client, _factory, await SeedAdministratorAsync());
        var hall = await SeedHallAsync(token);

        // Two sessions an hour apart, so the two directions cannot coincide.
        var earlyCode = $"SRT-E-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var lateCode = $"SRT-L-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await CreateSessionAsync(token, hall, earlyCode, SimfClock.Now.AddHours(1));
        await CreateSessionAsync(token, hall, lateCode, SimfClock.Now.AddHours(9));

        var ascending = await SortedCodesAsync(token, "start", descending: false);
        var descending = await SortedCodesAsync(token, "start", descending: true);

        Assert.Contains(earlyCode, ascending);
        Assert.Contains(lateCode, ascending);
        Assert.True(
            ascending.IndexOf(earlyCode) < ascending.IndexOf(lateCode),
            "Ascending by start must put the earlier session first.");
        Assert.True(
            descending.IndexOf(lateCode) < descending.IndexOf(earlyCode),
            "Descending by start must reverse it. If this fails the service is "
            + "matching a key the grid does not send and is falling through to its "
            + "default order.");
    }

    [Fact]
    public async Task Sessions_sort_on_end_orders_by_END_not_by_start()
    {
        var token = await AuthFlow.SignInControlPanelAsync(
            _client, _factory, await SeedAdministratorAsync());

        // Deliberately INVERTED: the one that starts first ends last. A switch that
        // silently falls through to "order by Start" therefore returns the opposite
        // order from the one asked for, which is what /admin/sessions did.
        //
        // Two halls, because the inversion requires the windows to overlap and a
        // single hall may not host two overlapping sessions
        // (SESSION_HALL_TIME_OVERLAP). The sort does not care which hall a row is in.
        var hallA = await SeedHallAsync(token);
        var hallB = await SeedHallAsync(token);
        var startsFirst = $"END-A-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var startsSecond = $"END-B-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var t0 = SimfClock.Now.AddDays(1);
        await CreateSessionAsync(token, hallA, startsFirst, t0, t0.AddHours(8));
        await CreateSessionAsync(token, hallB, startsSecond, t0.AddHours(1), t0.AddHours(2));

        var byEnd = await SortedCodesAsync(token, "end", descending: false);

        Assert.Contains(startsFirst, byEnd);
        Assert.Contains(startsSecond, byEnd);
        Assert.True(
            byEnd.IndexOf(startsSecond) < byEnd.IndexOf(startsFirst),
            "Ascending by end must put the session that ENDS first ahead of the one "
            + "that starts first. Getting the reverse means the grid was ordered by "
            + "Start instead.");
    }

    [Fact]
    public async Task Banners_sort_on_start_honours_the_descending_direction()
    {
        var token = await AuthFlow.SignInControlPanelAsync(
            _client, _factory, await SeedAdministratorAsync());

        var early = await SeedBannerAsync("srt-early", SimfClock.Now.AddDays(1));
        var late = await SeedBannerAsync("srt-late", SimfClock.Now.AddDays(9));

        var ascending = await SortedBannerTitlesAsync(token, "start", descending: false);
        var descending = await SortedBannerTitlesAsync(token, "start", descending: true);

        Assert.True(
            ascending.IndexOf(early) < ascending.IndexOf(late),
            "Ascending by start must put the earlier banner first.");
        Assert.True(
            descending.IndexOf(late) < descending.IndexOf(early),
            "Descending by start must reverse it. Both directions returning the same "
            + "order means the switch fell through to its DisplayOrder default.");
    }

    [Fact]
    public async Task Attendance_sorts_on_start_honours_the_descending_direction()
    {
        var token = await AuthFlow.SignInControlPanelAsync(
            _client, _factory, await SeedAdministratorAsync());
        var hall = await SeedHallAsync(token);

        var earlyCode = $"ATT-E-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var lateCode = $"ATT-L-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await CreateSessionAsync(token, hall, earlyCode, SimfClock.Now.AddHours(2));
        await CreateSessionAsync(token, hall, lateCode, SimfClock.Now.AddHours(10));

        // The attendance dashboard defaults to Start ascending, so only the
        // DESCENDING direction proves the key reached an arm: an unmatched key
        // falls through to that same ascending default and looks correct.
        var descending = await SortedAttendanceCodesAsync(token, "start", descending: true);

        Assert.Contains(earlyCode, descending);
        Assert.Contains(lateCode, descending);
        Assert.True(
            descending.IndexOf(lateCode) < descending.IndexOf(earlyCode),
            "Descending by start must put the later session first. Matching the "
            + "ascending default instead means the key reached no arm.");
    }

    [Fact]
    public async Task Operation_log_sorts_on_timestamp_honours_the_ascending_direction()
    {
        var token = await AuthFlow.SignInControlPanelAsync(
            _client, _factory, await SeedAdministratorAsync());

        // The audit log defaults to newest-first, so ASCENDING is the direction
        // that proves the arm exists — and it is the one that mattered: tracing
        // an incident forward from its start needs the oldest-first view.
        var oldest = await SeedOperationLogAsync(SimfClock.Now.AddDays(-9));
        var newest = await SeedOperationLogAsync(SimfClock.Now.AddDays(-1));

        var ascending = await SortedOperationLogEventsAsync(
            token, "timestamp", descending: false);

        Assert.Contains(oldest, ascending);
        Assert.Contains(newest, ascending);
        Assert.True(
            ascending.IndexOf(oldest) < ascending.IndexOf(newest),
            "Ascending by timestamp must put the OLDEST entry first. Getting the "
            + "newest means the key reached no arm and fell through to the "
            + "newest-first default, which is what made oldest-first unreachable.");
    }

    // ---- helpers -------------------------------------------------------------

    private async Task<string> SeedAdministratorAsync()
    {
        var email = $"gridsort-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var roles = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<SIMF.Domain.IdentityAccess.SimfRole>>();
        if (!await roles.RoleExistsAsync("Administrator"))
        {
            await roles.CreateAsync(new SIMF.Domain.IdentityAccess.SimfRole
            {
                Name = "Administrator",
                IsBaseline = true,
            });
        }

        var users = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<SIMF.Domain.IdentityAccess.SimfUser>>();
        var user = new SIMF.Domain.IdentityAccess.SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Grid Sort Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        await users.AddToRoleAsync(user, "Administrator");
        return email;
    }

    private async Task<AdminHallSummary> SeedHallAsync(string token)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = $"H{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                Name = "Sort Hall",
                NameArabic = "قاعة",
                Capacity = 50,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminHallSummary>>())!.Data!;
    }

    private async Task CreateSessionAsync(
        string token, AdminHallSummary hall, string code, DateTime start, DateTime? end = null)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code,
                Title = code,
                TitleArabic = code,
                // Event, not Session: a non-Event session must carry at least one
                // speaker (#4), and this fixture is about sort order, not speakers.
                Type = SessionType.Event,
                HallId = hall.Id,
                Start = start,
                End = end ?? start.AddHours(1),
            },
            token);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Creating session {code} failed: {(int)response.StatusCode} "
            + await response.Content.ReadAsStringAsync());
    }

    private async Task<string> SeedBannerAsync(string title, DateTime start)
    {
        var unique = $"{title}-{Guid.NewGuid():N}"[..20];
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        app.Banners.Add(new Banner
        {
            Id = Guid.NewGuid(),
            Title = unique,
            TitleArabic = unique,
            Start = start,
            End = start.AddDays(1),
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await app.SaveChangesAsync();
        return unique;
    }

    private async Task<List<string>> SortedCodesAsync(
        string token, string sort, bool descending)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/list",
            new GridQuery { Top = 200, Sort = sort, SortDescending = descending },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        return page.Items.Select(s => s.Code).ToList();
    }

    /// <summary>Returns the event type, which identifies the seeded row.</summary>
    private async Task<string> SeedOperationLogAsync(DateTime timestamp)
    {
        var eventType = "GridSort." + Guid.NewGuid().ToString("N")[..10];
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        app.OperationLog.Add(new SIMF.Domain.Auditing.OperationLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            Outcome = AuditOutcome.Success,
        });
        await app.SaveChangesAsync();
        return eventType;
    }

    private async Task<List<string>> SortedAttendanceCodesAsync(
        string token, string sort, bool descending)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/attendance/sessions/list",
            new GridQuery { Top = 200, Sort = sort, SortDescending = descending },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SIMF.Contracts.Attendance.SessionAttendanceRow>>>())!
            .Data!;
        return page.Items.Select(row => row.Code).ToList();
    }

    private async Task<List<string>> SortedOperationLogEventsAsync(
        string token, string sort, bool descending)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/operation-log/list",
            new GridQuery { Top = 200, Sort = sort, SortDescending = descending },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminOperationLogSummary>>>())!.Data!;
        return page.Items.Select(row => row.EventType).ToList();
    }

    private async Task<List<string>> SortedBannerTitlesAsync(
        string token, string sort, bool descending)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/banners/list",
            new GridQuery { Top = 200, Sort = sort, SortDescending = descending },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBannerSummary>>>())!.Data!;
        return page.Items.Select(b => b.Title).ToList();
    }

    private Task<HttpResponseMessage> PostAuthAsync(string url, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
