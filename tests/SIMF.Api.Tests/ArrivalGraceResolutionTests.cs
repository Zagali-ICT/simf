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

    // -- Helpers --------------------------------------------------------------

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
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
