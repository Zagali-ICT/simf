// GET /api/v1/admin/statistics/programme — the Control Panel's per-forum-day
// figures behind the 3-day dashboard chart.
//
// The load-bearing behaviour here is the DAY BOUNDARY. Instants are persisted as
// UTC but the forum's days are Saudi calendar days (UTC+03:00, no DST), so a
// record at 21:00 UTC already belongs to the NEXT Saudi day. Bucketing on the
// stored value would silently misfile every evening scan into the day before.
// Those tests are the reason this file exists.
//
// Isolation: the suite shares one database, so each test allocates its own
// never-reused three-day window (see NewProgrammeAsync) — records seeded by an
// earlier test cannot fall inside a later test's day windows.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Statistics;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class StatisticsProgrammeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string Endpoint = "/api/v1/admin/statistics/programme";

    /// <summary>Saudi Standard Time — UTC+03:00, no daylight saving, ever.</summary>
    private static readonly TimeSpan Ast = TimeSpan.FromHours(3);

    /// <summary>Hands each test its own never-reused block of dates. The suite
    /// shares one database, so a fixed set of dates would let scans seeded by an
    /// earlier test fall inside a later test's day windows and inflate its
    /// counts. Parallelisation is disabled assembly-wide (see AssemblyInfo), so
    /// a plain counter would do — Interlocked keeps it honest regardless.</summary>
    private static int _blockCounter = -1;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public StatisticsProgrammeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- The day boundary ---------------------------------------------------

    [Fact]
    public async Task A_record_just_after_midnight_saudi_counts_on_that_saudi_day()
    {
        // 00:30 Riyadh on day 2 is STORED as 00:30 on day 2 (owner decision
        // 2026-07-31 — Saudi local storage, no UTC). This case used to guard the
        // conversion trap: the same moment was stored as 21:30 UTC on day 1, so
        // bucketing on the raw value credited the visitor to the wrong forum day.
        // The trap is gone with the conversion, and the guard now pins WHY it is
        // gone — the stored date simply is the Saudi date.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var justAfterMidnight = SaudiAt(p.Day2, 0, 30);
        Assert.Equal(p.Day2, DateOnly.FromDateTime(justAfterMidnight));

        await SeedScanAsync(await SeedProfileAsync(), justAfterMidnight);

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Present);
        Assert.Equal(1, DayFor(days, p.Day2).Present);
        Assert.Equal(0, DayFor(days, p.Day3).Present);
    }

    [Fact]
    public async Task A_record_late_in_the_saudi_evening_stays_on_that_saudi_day()
    {
        // The other side of the boundary: 23:30 Riyadh on day 1 is 20:30 UTC,
        // still day 1. Guards against an over-correction that would push every
        // evening record forward a day.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedScanAsync(await SeedProfileAsync(), SaudiAt(p.Day1, 23, 30));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(1, DayFor(days, p.Day1).Present);
        Assert.Equal(0, DayFor(days, p.Day2).Present);
    }

    [Fact]
    public async Task A_hall_arrival_just_after_midnight_saudi_counts_on_that_saudi_day()
    {
        // The same boundary rule, applied to the Attended metric.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedHallArrivalAsync(Guid.NewGuid(), SaudiAt(p.Day3, 1, 15));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day2).Attended);
        Assert.Equal(1, DayFor(days, p.Day3).Attended);
    }

    [Fact]
    public async Task A_registration_just_after_midnight_saudi_counts_on_that_saudi_day()
    {
        // And to the Registered metric, which reads the Identity database.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedVisitorRegisteredAtAsync(SaudiAt(p.Day2, 0, 15));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Registered);
        Assert.Equal(1, DayFor(days, p.Day2).Registered);
    }

    [Fact]
    public async Task A_record_at_the_exact_start_of_a_saudi_day_counts_on_that_day()
    {
        // Exactly 00:00 Riyadh — the half-open window [start, end) must include
        // its own start instant, not hand it to the previous day.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedScanAsync(await SeedProfileAsync(), SaudiAt(p.Day2, 0, 0));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Present);
        Assert.Equal(1, DayFor(days, p.Day2).Present);
    }

    // -- Distinct counting --------------------------------------------------

    [Fact]
    public async Task Repeat_scans_by_one_visitor_count_once()
    {
        // "Present" is how many PEOPLE were let in, not how many scans happened.
        // A visitor who steps out for coffee and scans back in is still one person.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var profileId = await SeedProfileAsync();
        await SeedScanAsync(profileId, SaudiAt(p.Day2, 9));
        await SeedScanAsync(profileId, SaudiAt(p.Day2, 12));
        await SeedScanAsync(profileId, SaudiAt(p.Day2, 15));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(1, DayFor(days, p.Day2).Present);
    }

    [Fact]
    public async Task Two_visitors_on_the_same_day_both_count()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedScanAsync(await SeedProfileAsync(), SaudiAt(p.Day2, 10));
        await SeedScanAsync(await SeedProfileAsync(), SaudiAt(p.Day2, 11));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(2, DayFor(days, p.Day2).Present);
    }

    [Fact]
    public async Task The_same_visitor_on_two_days_counts_on_each()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var profileId = await SeedProfileAsync();
        await SeedScanAsync(profileId, SaudiAt(p.Day1, 10));
        await SeedScanAsync(profileId, SaudiAt(p.Day2, 10));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(1, DayFor(days, p.Day1).Present);
        Assert.Equal(1, DayFor(days, p.Day2).Present);
        Assert.Equal(0, DayFor(days, p.Day3).Present);
    }

    [Fact]
    public async Task Repeat_hall_arrivals_by_one_attendee_count_once()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var userId = Guid.NewGuid();
        await SeedHallArrivalAsync(userId, SaudiAt(p.Day3, 10));
        await SeedHallArrivalAsync(userId, SaudiAt(p.Day3, 14));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(1, DayFor(days, p.Day3).Attended);
    }

    // -- What must NOT count as present -------------------------------------

    [Fact]
    public async Task Denied_and_checkout_scans_do_not_count_as_present()
    {
        // A refused scan means the person did NOT get in, and a check-out is the
        // same person leaving — neither is an additional attendee.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedScanAsync(
            await SeedProfileAsync(), SaudiAt(p.Day1, 10), outcome: ScanOutcome.Denied);
        await SeedScanAsync(
            await SeedProfileAsync(), SaudiAt(p.Day1, 11), direction: ScanDirection.CheckOut);

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Present);
    }

    [Fact]
    public async Task A_scan_that_resolved_to_nobody_does_not_count_as_present()
    {
        // An unrecognised QR records a scan with no profile attached; it is not
        // a person who attended.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        await SeedScanAsync(profileId: null, SaudiAt(p.Day1, 10));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Present);
    }

    // -- Shape --------------------------------------------------------------

    [Fact]
    public async Task Every_active_day_is_returned_in_display_order_even_with_no_activity()
    {
        // A quiet day must still appear as a zero row. Dropping it would leave a
        // gap in the chart and make the forum look a day shorter than it is.
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(3, days.Count);
        Assert.Equal([p.Day1, p.Day2, p.Day3], days.Select(d => d.Date));
        Assert.Equal([1, 2, 3], days.Select(d => d.DisplayOrder));
        Assert.All(days, day =>
        {
            Assert.Equal(0, day.Registered);
            Assert.Equal(0, day.Present);
            Assert.Equal(0, day.Sessions);
            Assert.Equal(0, day.Attended);
        });
    }

    [Fact]
    public async Task Each_day_carries_its_bilingual_title()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal("Opening day", DayFor(days, p.Day1).Title);
        Assert.Equal("يوم الافتتاح", DayFor(days, p.Day1).TitleArabic);
    }

    [Fact]
    public async Task A_deactivated_day_is_excluded()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var day = await db.ProgrammeDays.SingleAsync(d => d.IsActive && d.Date == p.Day3);
            day.Deactivate();
            await db.SaveChangesAsync();
        }

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(2, days.Count);
        Assert.DoesNotContain(days, d => d.Date == p.Day3);
    }

    [Fact]
    public async Task Sessions_are_bucketed_onto_the_saudi_day_they_start_on()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var hallId = await SeedHallAsync();
        await SeedSessionAsync(hallId, SaudiAt(p.Day2, 9));
        await SeedSessionAsync(hallId, SaudiAt(p.Day2, 14));
        // A late-night session: 00:10 Riyadh on day 3, stored as 21:10 UTC on day 2.
        await SeedSessionAsync(hallId, SaudiAt(p.Day3, 0, 10));

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(2, DayFor(days, p.Day2).Sessions);
        Assert.Equal(1, DayFor(days, p.Day3).Sessions);
    }

    [Fact]
    public async Task A_deactivated_session_is_not_counted()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var p = await NewProgrammeAsync();

        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, SaudiAt(p.Day1, 10));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var session = await db.Sessions.SingleAsync(s => s.Id == sessionId);
            session.Deactivate();
            await db.SaveChangesAsync();
        }

        var days = await GetProgrammeDaysAsync(token);

        Assert.Equal(0, DayFor(days, p.Day1).Sessions);
    }

    [Fact]
    public async Task Headline_counts_are_present_and_non_negative()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var programme = await GetProgrammeAsync(token);

        Assert.True(programme.CurrentUsers > 0, "the signed-in admin alone makes this non-zero");
        Assert.True(programme.Visitors >= 0);
        Assert.True(programme.Staff >= 0);
        Assert.True(programme.Moderators >= 0);
        Assert.True(programme.Exhibitors >= 0);
        Assert.True(programme.ExhibitorAccounts >= 0);
        Assert.True(programme.Sponsors >= 0);
        Assert.True(programme.Speakers >= 0);
        Assert.True(programme.Booths >= 0);
        Assert.True(programme.TotalAttended >= 0);
    }

    [Fact]
    public async Task Staff_are_counted_from_the_profile_types_mobile_app_role()
    {
        // The staff / exhibitor mapping is admin-curated data, not a hardcoded
        // role name — a profile type set to Staff must move the count.
        var token = await CreateAdministratorAndSignInAsync();
        var before = await GetProgrammeAsync(token);

        await SeedProfileAsync(MobileAppRole.Staff);

        var after = await GetProgrammeAsync(token);

        Assert.Equal(before.Staff + 1, after.Staff);
        Assert.Equal(before.Moderators, after.Moderators);
    }

    [Fact]
    public async Task Exhibitor_accounts_are_counted_separately_from_exhibitor_companies()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var before = await GetProgrammeAsync(token);

        await SeedProfileAsync(MobileAppRole.Exhibitor);

        var after = await GetProgrammeAsync(token);

        Assert.Equal(before.ExhibitorAccounts + 1, after.ExhibitorAccounts);
        // The company list is untouched by adding an account.
        Assert.Equal(before.Exhibitors, after.Exhibitors);
    }

    // -- Authorisation ------------------------------------------------------

    [Fact]
    public async Task Anonymous_caller_is_rejected()
    {
        var response = await _client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync(Endpoint, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ------------------------------------------------------------

    /// <summary>The three forum dates allocated to one test.</summary>
    private sealed record Programme(DateOnly Day1, DateOnly Day2, DateOnly Day3);

    /// <summary>The stored UTC instant for a Saudi wall-clock time on a given
    /// forum day — lets a test place a record precisely either side of the
    /// 21:00 UTC day boundary.</summary>
    private static DateTime SaudiAt(DateOnly day, int hour, int minute = 0) =>
        day.ToDateTime(new TimeOnly(hour, minute));

    private static ProgrammeDayStats DayFor(
        IReadOnlyList<ProgrammeDayStats> days, DateOnly date) =>
        days.Single(d => d.Date == date);

    /// <summary>
    /// Deactivates every existing programme day, then seeds a fresh three-day
    /// forum on dates no other test has used.
    ///
    /// <para>Two separate isolation problems, two separate fixes: deactivating
    /// clears the <i>day set</i> so a count-the-rows assertion is exact, and the
    /// unique dates keep this test's <i>metric windows</i> free of records that
    /// earlier tests seeded.</para>
    /// </summary>
    private async Task<Programme> NewProgrammeAsync()
    {
        var index = Interlocked.Increment(ref _blockCounter);
        // A ten-day stride leaves a wide margin either side of each block.
        var day1 = new DateOnly(2030, 1, 1).AddDays(index * 10);
        var programme = new Programme(day1, day1.AddDays(1), day1.AddDays(2));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var existing = await db.ProgrammeDays.Where(d => d.IsActive).ToListAsync();
        foreach (var day in existing)
        {
            day.Deactivate();
        }

        var now = SimfClock.Now;
        db.ProgrammeDays.AddRange(
            new ProgrammeDay
            {
                Id = Guid.NewGuid(),
                Date = programme.Day1,
                Title = "Opening day",
                TitleArabic = "يوم الافتتاح",
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = now,
            },
            new ProgrammeDay
            {
                Id = Guid.NewGuid(),
                Date = programme.Day2,
                Title = "Main day",
                TitleArabic = "اليوم الرئيسي",
                DisplayOrder = 2,
                IsActive = true,
                CreatedAt = now,
            },
            new ProgrammeDay
            {
                Id = Guid.NewGuid(),
                Date = programme.Day3,
                Title = "Closing day",
                TitleArabic = "يوم الختام",
                DisplayOrder = 3,
                IsActive = true,
                CreatedAt = now,
            });

        await db.SaveChangesAsync();
        return programme;
    }

    private async Task<StatisticsProgramme> GetProgrammeAsync(string token)
    {
        var response = await GetAuthAsync(Endpoint, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<StatisticsProgramme>>())!.Data!;
    }

    private async Task<IReadOnlyList<ProgrammeDayStats>> GetProgrammeDaysAsync(string token) =>
        (await GetProgrammeAsync(token)).Days;

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedProfileAsync(MobileAppRole role = MobileAppRole.None)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Type " + Guid.NewGuid().ToString("N")[..6],
            NameArabic = "نوع",
            PageColor = "#0EA5E9",
            IsForVisitor = role == MobileAppRole.None,
            MobileAppRole = role,
            IsActive = true,
            CreatedAt = now,
        };
        db.ProfileTypes.Add(profileType);

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProfileTypeId = profileType.Id,
            Name = "Attendee",
            NameArabic = "حاضر",
            NationalityId = 682,
            IsActive = true,
            CreatedAt = now,
        };
        db.UserProfiles.Add(profile);

        await db.SaveChangesAsync();
        return profile.Id;
    }

    private async Task<Guid> SeedGateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = "G-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Stats Gate",
            NameArabic = "بوابة الإحصاءات",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Gates.Add(gate);
        await db.SaveChangesAsync();
        return gate.Id;
    }

    private async Task SeedScanAsync(
        Guid? profileId,
        DateTime scannedAtUtc,
        ScanOutcome outcome = ScanOutcome.Allowed,
        ScanDirection direction = ScanDirection.CheckIn)
    {
        var gateId = await SeedGateAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        db.GateScans.Add(new GateScan
        {
            GateId = gateId,
            UserProfileId = profileId,
            ScannedByUserId = Guid.NewGuid(),
            ScannedAt = scannedAtUtc,
            Direction = direction,
            Outcome = outcome,
            DenialReasonCode = outcome == ScanOutcome.Denied
                ? DenialReasonCode.HolderNotApproved
                : null,
            QrIdAtScan = "QR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Source = ScanSource.Simulator,
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
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Stats Hall",
            NameArabic = "قاعة الإحصاءات",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<Guid> SeedSessionAsync(Guid hallId, DateTime startUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "S-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Stats Session",
            TitleArabic = "جلسة الإحصاءات",
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

    private async Task SeedHallArrivalAsync(Guid userId, DateTime enterUtc)
    {
        var hallId = await SeedHallAsync();
        var sessionId = await SeedSessionAsync(hallId, enterUtc);

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
            CreatedAt = SimfClock.Now,
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedVisitorRegisteredAtAsync(DateTime createdAtUtc)
    {
        var email = $"stats-reg-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Stats Registrant",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        // The audit interceptor stamps CreatedAt at insert; the registration
        // metric needs a controlled instant, so set it explicitly afterwards.
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var stored = await identity.Users.SingleAsync(u => u.Id == user.Id);
        stored.CreatedAt = createdAtUtc;
        await identity.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"stats-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Stats Admin",
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
