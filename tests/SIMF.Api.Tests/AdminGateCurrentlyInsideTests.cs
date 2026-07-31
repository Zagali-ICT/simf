// Tests cover: GET /api/v1/admin/gates/reports/currently-inside — the report
// behind the Control Panel's Gates Operations dashboard (/admin/gates/dashboard).
//
// WHY THIS FILE EXISTS. The endpoint returned 500 on EVERY request, including
// against an empty GateScans table, and shipped that way. The service built the
// "latest allowed scan per visitor" set as a filter over a GroupBy projection:
//
//     .GroupBy(s => s.UserProfileId!.Value)
//     .Select(g => new { UserProfileId = g.Key,
//                        Last = g.OrderByDescending(s => s.ScannedAt).First() })
//     .Where(x => x.Last.Direction == ScanDirection.CheckIn && ...)
//
// EF Core cannot translate a predicate over a member of an entity projected out
// of a grouping: it throws KeyNotFoundException('EmptyProjectionMember') while
// building the SQL. Because that happens at TRANSLATION time it is independent of
// the data, so the failure is total rather than intermittent — and equally, no
// amount of seeding would have masked it.
//
// It reached production because nothing ever executed this method against a
// relational provider. A LINQ-to-objects run (an in-memory provider, or a unit
// test over a List<GateScan>) evaluates the same expression tree happily; only
// SQL translation rejects it. So the first test below is the one that matters:
// it asks for the report and asserts 200. The rest pin the semantics, because the
// fix changed the query's SHAPE (to a correlated NOT EXISTS) and "no longer
// throws" is not the same claim as "still returns the right visitors".
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminGateCurrentlyInsideTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminGateCurrentlyInsideTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>The regression proper. This asserts nothing about the CONTENT of
    /// the report — only that asking for it does not fail. That is the whole bug:
    /// the query could not be translated, so the endpoint 500'd whatever the scan
    /// log held. Do not "strengthen" this into a data assertion; its value is that
    /// it passes on an untouched database.</summary>
    [Fact]
    public async Task Currently_inside_report_is_translatable_and_returns_200()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var response = await GetAuthAsync(
            "/api/v1/admin/gates/reports/currently-inside", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>>();
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
    }

    [Fact]
    public async Task A_visitor_whose_latest_allowed_scan_is_a_check_in_is_inside()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var gate = await CreateGateAsync(token);
        var (qr, profileId) = await CreateApprovedVisitorWithQrAsync("Inside Visitor");

        await RecordScanAsync(gate.Id, qr, token); // first scan at a Both gate = CheckIn

        var rows = await GetCurrentlyInsideAsync(token);
        var row = Assert.Single(rows, r => r.UserProfileId == profileId);
        Assert.Equal(gate.Id, row.LastCheckInGateId);
        Assert.Equal(gate.Code, row.LastCheckInGateCode);
    }

    /// <summary>The half a "does it throw" test cannot reach: the rewritten query
    /// must still let a LATER scan overrule an earlier one. A visitor who checked
    /// in and then out is outside, even though the check-in row is still there and
    /// still matches "allowed check-in inside the presence window".</summary>
    [Fact]
    public async Task A_visitor_who_scanned_out_again_is_no_longer_inside()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var gate = await CreateGateAsync(token);
        var (qr, profileId) = await CreateApprovedVisitorWithQrAsync("Departed Visitor");

        await SeedScanAsync(gate.Id, profileId, qr, ScanDirection.CheckIn,
            SimfClock.Now.AddMinutes(-10));
        var inside = await GetCurrentlyInsideAsync(token);
        Assert.Contains(inside, r => r.UserProfileId == profileId);

        // Five minutes AFTER the check-in, so it is unambiguously the later row.
        await SeedScanAsync(gate.Id, profileId, qr, ScanDirection.CheckOut,
            SimfClock.Now.AddMinutes(-5));

        var after = await GetCurrentlyInsideAsync(token);
        Assert.DoesNotContain(after, r => r.UserProfileId == profileId);
    }

    /// <summary>A check-in older than the 16-hour presence window is treated as
    /// departed even with no check-out — the in-only-gate reconciliation the
    /// original comment describes. Worth pinning because the window moved from a
    /// post-projection filter into the main WHERE.</summary>
    [Fact]
    public async Task A_check_in_older_than_the_presence_window_is_not_inside()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var gate = await CreateGateAsync(token);
        var (qr, profileId) = await CreateApprovedVisitorWithQrAsync("Stale Visitor");

        // 20 hours ago — outside the 16-hour StalePresenceWindow.
        await SeedScanAsync(gate.Id, profileId, qr, ScanDirection.CheckIn,
            SimfClock.Now.AddHours(-20));

        var rows = await GetCurrentlyInsideAsync(token);
        Assert.DoesNotContain(rows, r => r.UserProfileId == profileId);
    }

    /// <summary>Each visitor appears at most once. The GroupBy form got this free;
    /// the correlated NOT EXISTS only gets it because of the Id tiebreak, so it is
    /// worth an explicit assertion — including the tie itself, where two allowed
    /// scans share a ScannedAt to the tick and only the Id can separate them.</summary>
    [Fact]
    public async Task Each_visitor_appears_at_most_once_even_when_scans_tie_on_time()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var gate = await CreateGateAsync(token);
        var (qr, profileId) = await CreateApprovedVisitorWithQrAsync("Repeat Visitor");

        // Two check-ins at the SAME instant — one DateTime value reused, not
        // two UtcNow reads, which would differ by milliseconds and not tie at all.
        // Without the Id tiebreak neither row has a strictly-later sibling, so both
        // qualify and the dashboard shows the visitor twice.
        var sameInstant = SimfClock.Now.AddMinutes(-3);
        await SeedScanAsync(gate.Id, profileId, qr, ScanDirection.CheckIn, sameInstant);
        await SeedScanAsync(gate.Id, profileId, qr, ScanDirection.CheckIn, sameInstant);

        var rows = await GetCurrentlyInsideAsync(token);
        Assert.Single(rows, r => r.UserProfileId == profileId);
    }

    /// <summary>Writes a GateScan row straight to the database.
    ///
    /// <para>Deliberately NOT via the scan endpoint. GateOperatorService absorbs a
    /// repeat allowed scan for the same visitor inside a 5-second DuplicateWindow
    /// (G-5) — a real anti-button-mash rule — so a test that posts two scans back
    /// to back writes ONE row and silently proves nothing. That is exactly how the
    /// first draft of the check-out test below "passed". These tests are about the
    /// REPORT query, not the scan engine, so they seed the scan log directly and
    /// place each row on the clock where the assertion needs it.</para>
    ///
    /// <para>Takes an ABSOLUTE <paramref name="scannedAt"/> rather than an offset,
    /// so two rows can be given the identical instant and genuinely tie.</para></summary>
    private async Task SeedScanAsync(
        Guid gateId, Guid profileId, string qr, ScanDirection direction,
        DateTime scannedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.GateScans.Add(new GateScan
        {
            GateId = gateId,
            UserProfileId = profileId,
            QrIdAtScan = qr,
            Direction = direction,
            Outcome = ScanOutcome.Allowed,
            Source = ScanSource.Simulator,
            ScannedByUserId = Guid.Empty,
            ScannedAt = scannedAt,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<AdminCurrentlyInsideRow>> GetCurrentlyInsideAsync(
        string token)
    {
        var response = await GetAuthAsync(
            "/api/v1/admin/gates/reports/currently-inside", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>>())!.Data!;
    }

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken)
    {
        var code = $"GCI-{Guid.NewGuid().ToString("N")[..8]}";
        var actorId = GetUserIdFromToken(adminToken);
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Currently-inside Test Gate",
                NameArabic = "بوابة اختبار الموجودين",
                DirectionMode = DirectionMode.Both,
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = new List<Guid> { actorId },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<(string Qr, Guid ProfileId)> CreateApprovedVisitorWithQrAsync(
        string displayName)
    {
        var email = $"inside-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var profileId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = profileId,
            UserId = user.Id,
            QrId = qrId,
            NameArabic = displayName,
            Name = displayName,
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return (qrId, profileId);
    }

    private async Task RecordScanAsync(Guid gateId, string qr, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new GateScanRequest
            {
                Qr = qr,
                Source = ScanSource.Simulator,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"inside-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Currently-inside Test Admin",
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

    private static Guid GetUserIdFromToken(string token)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
        return Guid.Parse(jwt.Claims.First(c => c.Type == "sub" || c.Type == "nameid").Value);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
