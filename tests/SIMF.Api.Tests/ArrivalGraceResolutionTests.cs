// D-839 — the arrival grace resolves session override -> hall -> global -> 15.
//
// These are deliberately BEHAVIOURAL: every case drives the real hall-door
// endpoint and asserts whether the door opened, rather than asserting what a
// private helper returned. A resolution chain that is only unit-tested proves
// the arithmetic and not the door, and the door is the whole point.
//
// The fixed geometry: a session that starts 40 minutes from now. Under the
// historical 15-minute grace it is NOT admitting, so every "admitted" result
// below is caused by the layer under test and nothing else.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ArrivalGraceResolutionTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    /// <summary>Far enough ahead that the default 15-minute grace refuses it, and
    /// comfortably inside the 60 the widening cases set.</summary>
    private static readonly TimeSpan StartsIn = TimeSpan.FromMinutes(40);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ArrivalGraceResolutionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task With_no_grace_configured_a_session_40_minutes_out_is_closed()
    {
        // The baseline the other cases are measured against. Nothing set anywhere,
        // so the global default (15) applies and the doors are shut.
        var (sessionId, qrId, token) = await SeedAsync(hallGrace: null, sessionGrace: null);

        var response = await ScanAsync(sessionId, qrId, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotLive, body.Error!.Code);
    }

    [Fact]
    public async Task A_hall_grace_opens_a_session_the_default_would_refuse()
    {
        // The queue-before-a-keynote case, solved WITHOUT arming the global
        // walk-in capability (which is a server-access switch that also relaxes
        // an approval gate).
        var (sessionId, qrId, token) = await SeedAsync(hallGrace: 60, sessionGrace: null);

        var response = await ScanAsync(sessionId, qrId, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
        Assert.True(result.Status.Arrived);
    }

    [Fact]
    public async Task A_session_override_opens_a_session_its_hall_would_refuse()
    {
        // One keynote widens its own doors; the hall it runs in is untouched.
        var (sessionId, qrId, token) = await SeedAsync(hallGrace: null, sessionGrace: 60);

        var response = await ScanAsync(sessionId, qrId, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_session_override_of_zero_beats_a_wide_hall_grace()
    {
        // The precedence test that actually bites. A wide hall grace WOULD admit
        // this session, and the session's own 0 overrules it — which only passes
        // if the override is read as "0 minutes" rather than as "unset".
        // Written this way on purpose: `?? hall` on an int? is correct, but the
        // same intent expressed as a truthiness or `> 0` check would silently
        // treat a deliberate 0 as "inherit", and this is the case that catches it.
        var (sessionId, qrId, token) = await SeedAsync(hallGrace: 60, sessionGrace: 0);

        var response = await ScanAsync(sessionId, qrId, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotLive, body.Error!.Code);
    }

    [Fact]
    public async Task A_hall_grace_of_zero_beats_the_global_default()
    {
        // Same trap one layer down: an explicit hall 0 must not fall through to
        // the global 15. A session starting in 8 minutes is inside the default
        // grace, so it would be admitted if the 0 were mistaken for "unset".
        var (sessionId, qrId, token) = await SeedAsync(
            hallGrace: 0, sessionGrace: null, startsIn: TimeSpan.FromMinutes(8));

        var response = await ScanAsync(sessionId, qrId, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_admin_session_row_reports_the_grace_the_door_will_use()
    {
        // The Hall-Arrivals console filters its session picker by this number, so
        // if it disagreed with the door the console would hide sessions the server
        // would happily admit — the D-839 defect, in the surface that showed it.
        var (sessionId, _, _) = await SeedAsync(hallGrace: 60, sessionGrace: null);
        var (otherSessionId, _, _) = await SeedAsync(hallGrace: 60, sessionGrace: 5);
        var adminToken = await CreateAdministratorAndSignInAsync();

        var rows = await ListSessionsAsync(adminToken);

        Assert.Equal(60, Row(rows, sessionId).EffectiveArrivalGraceMinutes);
        Assert.Null(Row(rows, sessionId).ArrivalGraceMinutesOverride);
        // The override wins, and the raw value round-trips for the Excel lane.
        Assert.Equal(5, Row(rows, otherSessionId).EffectiveArrivalGraceMinutes);
        Assert.Equal(5, Row(rows, otherSessionId).ArrivalGraceMinutesOverride);
    }

    [Theory]
    [InlineData(60, null, 60)]   // the hall's grace, inherited
    [InlineData(60, 5, 5)]       // the session's own override wins
    [InlineData(60, 0, 0)]       // a deliberate zero is honoured, not "unset"
    [InlineData(null, null, 15)] // nothing set anywhere: the historical default
    public async Task The_public_session_detail_carries_the_resolved_grace(
        int? hallGrace, int? sessionGrace, int expected)
    {
        // D-840 — the app decides from this whether to show its "you can check
        // in now" strip. It used to hard-code 15 under a comment telling the
        // next person to keep it in step with a server constant by hand; D-839
        // removed the constant there was to mirror, so the server sends the
        // answer instead. Anonymous, like the rest of the public programme read.
        var (sessionId, _, _) = await SeedAsync(hallGrace, sessionGrace);

        using var response = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionGraceRow>>())!.Data!;
        Assert.Equal(expected, detail.ArrivalGraceMinutes);
    }

    /// <summary>Just the field under test, so a later append to
    /// <c>PublicSessionDetail</c> cannot break this test's deserialisation.</summary>
    private sealed record PublicSessionGraceRow(int ArrivalGraceMinutes);

    [Fact]
    public void The_bound_still_matches_the_one_baked_into_the_shipped_migration()
    {
        // D-839 — the EF configuration interpolates MaxArrivalGraceMinutes into
        // CK_Halls_ArrivalGrace / CK_Sessions_ArrivalGrace, but the SHIPPED
        // migration baked the literal 240, as migrations must. So raising the
        // constant alone would leave the API, both CP forms and both Excel
        // importers accepting a value the deployed database rejects — surfacing
        // as a constraint violation, i.e. a 500, on a hall an admin just saved.
        // Nothing else fails the build when that happens; this does.
        //
        // Raising the bound is legitimate — it just needs a new migration in the
        // same changeset, and updating this number is the reminder to write one.
        Assert.Equal(240, WalkInModeOptions.MaxArrivalGraceMinutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(241)]
    public async Task A_hall_grace_outside_the_bound_is_refused(int minutes)
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync("/api/v1/admin/halls", new
        {
            code = "HG-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            name = "Bound Hall",
            nameArabic = "قاعة الحد",
            capacity = 50,
            arrivalGraceMinutes = minutes,
        }, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_edit_can_set_an_override_and_the_door_honours_it()
    {
        // D-842 — the whole feature was unreachable from its only UI. D-839 added
        // the field to the contract DTO, the Control Panel and the service, but the
        // API's own route DTO (UpdateSessionRequest) omitted it, so the PUT bound
        // null and the mapping passed null on. The CP reported "Session … was
        // updated" and the column stayed NULL.
        //
        // Asserted through the DOOR rather than the stored row: the point of the
        // setting is that the hall admits earlier, and only a scan proves the edit
        // travelled the whole way instead of merely landing in a column.
        var (sessionId, qrId, operatorToken) =
            await SeedAsync(hallGrace: null, sessionGrace: null);
        var adminToken = await CreateAdministratorAndSignInAsync();

        // 40 minutes out with nothing configured: the default 15 keeps it shut.
        using (var before = await ScanAsync(sessionId, qrId, operatorToken))
        {
            Assert.Equal(HttpStatusCode.Conflict, before.StatusCode);
        }

        using var update = await PutSessionAsync(
            sessionId, adminToken, arrivalGraceMinutesOverride: 60);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var after = await ScanAsync(sessionId, qrId, operatorToken);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var result = (await after.Content
            .ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
        Assert.True(result.Status.Arrived);
    }

    [Fact]
    public async Task An_override_survives_an_edit_that_does_not_change_it()
    {
        // The second half of the same defect, and the more dangerous half: the CP
        // loads the stored override into its form and echoes it back on every save,
        // so before the fix editing ANYTHING else on the session silently reset the
        // override to null. The doors then quietly narrowed back to the hall's
        // grace with no error, no toast and nothing in the audit trail to read.
        var (sessionId, _, _) = await SeedAsync(hallGrace: null, sessionGrace: 60);
        var adminToken = await CreateAdministratorAndSignInAsync();

        using var response = await PutSessionAsync(
            sessionId, adminToken, arrivalGraceMinutesOverride: 60);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await ListSessionsAsync(adminToken);
        Assert.Equal(60, Row(rows, sessionId).ArrivalGraceMinutesOverride);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Edits a seeded session the way the Control Panel does: every stored
    /// scalar echoed back, with the arrival-grace override as the value under test.
    /// Reading the row first is what makes this a real round-trip rather than a
    /// create in disguise.</summary>
    private async Task<HttpResponseMessage> PutSessionAsync(
        Guid sessionId, string token, int? arrivalGraceMinutesOverride)
    {
        Session stored;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            stored = await db.Sessions.AsNoTracking()
                .SingleAsync(session => session.Id == sessionId);
        }

        return await PutAuthAsync($"/api/v1/admin/sessions/{sessionId}", new
        {
            code = stored.Code,
            title = stored.Title,
            titleArabic = stored.TitleArabic,
            hallId = stored.HallId,
            start = stored.Start,
            end = stored.End,
            isActive = stored.IsActive,
            arrivalGraceMinutesOverride,
        }, token);
    }

    private static AdminSessionSummaryRow Row(
        IReadOnlyList<AdminSessionSummaryRow> rows, Guid sessionId) =>
        rows.Single(row => row.Id == sessionId);

    /// <summary>The two fields under test, read off the admin list response. A
    /// local shape rather than the contract record so a future append to
    /// <c>AdminSessionSummary</c> cannot break this test's deserialisation.</summary>
    private sealed record AdminSessionSummaryRow(
        Guid Id,
        int? ArrivalGraceMinutesOverride,
        int EffectiveArrivalGraceMinutes);

    private async Task<IReadOnlyList<AdminSessionSummaryRow>> ListSessionsAsync(string token)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new { top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummaryRow>>>())!;
        return body.Data!.Items;
    }

    private Task<HttpResponseMessage> ScanAsync(Guid sessionId, string qrId, string token) =>
        PostAuthAsync($"/api/v1/admin/sessions/{sessionId}/arrivals",
            new RecordQrArrivalRequest { QrId = qrId }, token);

    private async Task<(Guid SessionId, string QrId, string OperatorToken)> SeedAsync(
        int? hallGrace, int? sessionGrace, TimeSpan? startsIn = null)
    {
        var operatorToken = await CreateAdministratorAndSignInAsync();
        var start = SimfClock.Now + (startsIn ?? StartsIn);

        Guid sessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var hall = new Hall
            {
                Id = Guid.NewGuid(),
                Code = "GR-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Grace Hall",
                NameArabic = "قاعة المهلة",
                Capacity = 100,
                IsActive = true,
                CreatedAt = SimfClock.Now,
                ArrivalGraceMinutes = hallGrace,
            };
            db.Halls.Add(hall);
            var session = new Session
            {
                Id = Guid.NewGuid(),
                Code = "GRS-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Grace Session",
                TitleArabic = "جلسة المهلة",
                HallId = hall.Id,
                Start = start,
                End = start.AddHours(1),
                IsActive = true,
                CreatedAt = SimfClock.Now,
                ArrivalGraceMinutesOverride = sessionGrace,
            };
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        var qrId = await CreateApprovedVisitorWithQrAsync();
        return (sessionId, qrId, operatorToken);
    }

    private async Task<string> CreateApprovedVisitorWithQrAsync()
    {
        var email = $"grace-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Grace Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            NameArabic = "زائر المهلة",
            Name = "Grace Visitor",
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            // Admission is decided on the PROFILE, so approving only the account
            // above would leave every door refusing this badge.
            AdmissionState = AccountState.Approved,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"grace-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Grace Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class =>
        SendAuthAsync(HttpMethod.Post, url, body, token);

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class =>
        SendAuthAsync(HttpMethod.Put, url, body, token);

    private Task<HttpResponseMessage> SendAuthAsync<TBody>(
        HttpMethod method, string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
