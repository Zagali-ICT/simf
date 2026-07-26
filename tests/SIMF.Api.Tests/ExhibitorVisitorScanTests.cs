// D-426 — exhibitor lead capture: scan a visitor badge → capture to My Visitors
// + return the full card.
// DEF-EXH-001 — only a profile type carrying MobileAppRole.Exhibitor may scan
//               (Staff / Moderator / plain Visitor are all 403).
// DEF-EXH-003 — the scanned SUBJECT must be an active audience-side account;
//               a partner badge answers the same 404 as an unknown code.
// DEF-EXH-002 — a NEW capture notifies the visitor once, naming the exhibitor;
//               an idempotent re-scan raises nothing.
// DEF-EXH-004 — the subject test also runs on the READ path, so a row captured
//               while the old rule was in force stops projecting a card.
// DEF-EXH-005 — a booth officer provisioned from the CP can actually scan.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExhibitorVisitorScanTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorVisitorScanTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exhibitor_scans_visitor_badge_captures_and_returns_full_card()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Booth", "جناح أكمي");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "BADGE2026XYZ", "Visitor One", "الزائر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "badge2026xyz", Note = "booth A3" },
            exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var card = (await scan.Content.ReadFromJsonAsync<ApiResult<VisitorCard>>())!.Data!;
        Assert.Equal("Visitor One", card.Name);
        Assert.True(card.Available);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Single(rows);
        Assert.Equal("Visitor One", rows[0].Card.Name);
        Assert.Equal("booth A3", rows[0].Note);
    }

    // DEF-EXH-009 — the caller here now carries a REAL audience profile type
    // ("Normal"), so the eligibility branch is exercised rather than the old
    // "no ProfileTypeId at all" shortcut that never reached it.
    [Fact]
    public async Task Visitor_caller_cannot_scan_badges_403()
    {
        var (visitorToken, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "SELFBADGE", "Self", "نفسي");

        var (_, targetId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(targetId, "OTHERBADGE", "Other", "آخر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "OTHERBADGE" }, visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // DEF-EXH-001 — a Staff token is a partner (IsForVisitor=false) profile type,
    // which the old "not a visitor type" test admitted. It must be refused.
    [Fact]
    public async Task Staff_caller_cannot_scan_badges_403()
    {
        var (staffToken, staffId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(staffId, "Staff", "Gate Officer", "موظف البوابة");

        var (_, targetId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(targetId, "STAFFTARGET", "Target", "هدف");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "STAFFTARGET" }, staffToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", staffToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // DEF-EXH-001 — same for a Moderator token.
    [Fact]
    public async Task Moderator_caller_cannot_scan_badges_403()
    {
        var (moderatorToken, moderatorId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(moderatorId, "Moderator", "Desk Lead", "منسّق");

        var (_, targetId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(targetId, "MODTARGET", "Target", "هدف");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "MODTARGET" }, moderatorToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", moderatorToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Unknown_badge_returns_404()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Empty Booth", "جناح");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "NOSUCHBADGE" }, exhibitorToken);
        Assert.Equal(HttpStatusCode.NotFound, scan.StatusCode);
    }

    // DEF-EXH-003 — a partner badge (staff / another exhibitor) and a deactivated
    // profile are not capturable, and both answer the SAME 404 as an unknown code
    // so the scan never leaks that the badge exists.
    [Fact]
    public async Task Ineligible_badge_subject_returns_404()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Booth", "جناح أكمي");

        var (_, staffId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(staffId, "Staff", "Gate Officer", "موظف", "STAFFBADGE1");

        var (_, otherExhibitorId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(
            otherExhibitorId, "Exhibitor", "Rival Booth", "جناح منافس", "RIVALBADGE1");

        var (_, retiredId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(retiredId, "RETIREDBADGE", "Retired", "متقاعد");
        await DeactivateProfileAsync(retiredId);

        foreach (var badge in new[] { "STAFFBADGE1", "RIVALBADGE1", "RETIREDBADGE" })
        {
            var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
                new ScanVisitorBadgeRequest { QrId = badge }, exhibitorToken);
            Assert.Equal(HttpStatusCode.NotFound, scan.StatusCode);
        }

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitorToken);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Empty(rows);
    }

    // DEF-EXH-002 — the visitor is told, in-app, that their card was shared and
    // WITH WHOM. Exactly one notification per new capture; the idempotent re-scan
    // (which only refreshes the note) raises none.
    [Fact]
    public async Task New_capture_notifies_the_visitor_once_and_a_rescan_is_silent()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Marine", "أكمي البحرية");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "NOTIFYBADGE", "Visitor Two", "الزائر");

        var first = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "NOTIFYBADGE" }, exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var notification = await SingleCaptureNotificationAsync(visitorId);
        Assert.Contains("Acme Marine", notification.Body);
        Assert.Contains("أكمي البحرية", notification.BodyArabic);

        var second = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "NOTIFYBADGE", Note = "second pass" },
            exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Still exactly one — the re-scan refreshed the note without re-notifying.
        await SingleCaptureNotificationAsync(visitorId);
    }

    // DEF-EXH-005 — the CP's own provisioning path (POST
    // /admin/exhibitors/{id}/accounts) used to create the booth officer with NO
    // profile type, which the MobileAppRole.Exhibitor rule can never admit: the
    // CP produced exhibitors that could not scan. The account must come out of
    // the pipeline able to use the tools it was provisioned for.
    [Fact]
    public async Task Cp_provisioned_booth_officer_can_scan_and_list()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync();

        var officerEmail = $"booth-{Guid.NewGuid():N}@simf.test";
        var provision = await PostAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts",
            new ProvisionExhibitorAccountRequest
            {
                ContactName = "Booth Officer",
                Email = officerEmail,
                RoleLabel = "Stand lead",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, provision.StatusCode);
        var account = (await provision.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Data!;

        // The invite flow sets the password out-of-band; the desk approves the
        // account. Both are done directly here so the test stays on the scan.
        var officerToken = await SignInProvisionedAccountAsync(officerEmail);

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "BOOTHBADGE1", "Lead One", "زائر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "BOOTHBADGE1" }, officerToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", officerToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Equal("Lead One", Assert.Single(rows).Card.Name);

        // The provisioned account is the exhibitor profile type, resolved by its
        // MobileAppRole (never by a name literal) — the same column the scan
        // authorises on.
        Assert.Equal(
            MobileAppRole.Exhibitor, await AssignedMobileAppRoleAsync(account.UserId));
    }

    // DEF-EXH-004 — a row captured while the vulnerable rule was in force (no
    // subject test at all) must stop projecting the subject's live card: the
    // exhibitor here legitimately holds a capture of a STAFF account and of a
    // since-deactivated visitor, and neither may be listed.
    [Fact]
    public async Task Legacy_captures_of_ineligible_subjects_are_not_listed()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Legacy Booth", "جناح قديم");

        var (_, staffId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(staffId, "Staff", "Gate Officer", "موظف", "LEGACYSTAFF");

        var (_, retiredId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(retiredId, "LEGACYRETIRED", "Retired", "متقاعد");
        await DeactivateProfileAsync(retiredId);

        var (_, keptId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(keptId, "LEGACYKEPT", "Still A Visitor", "زائر");

        await SeedLegacyCaptureAsync(exhibitorId, staffId);
        await SeedLegacyCaptureAsync(exhibitorId, retiredId);
        await SeedLegacyCaptureAsync(exhibitorId, keptId);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;

        // Only the still-eligible audience subject survives; no PII (email or
        // either mobile number) is projected for the other two.
        var only = Assert.Single(rows);
        Assert.Equal("Still A Visitor", only.Card.Name);
        Assert.DoesNotContain(rows, row => row.Card.UserId == staffId);
        Assert.DoesNotContain(rows, row => row.Card.UserId == retiredId);
    }

    // DEF-EXH-001 (read path) — the caller test guards the list endpoint too, so a
    // Staff token cannot read back rows it captured while the old rule let it scan.
    [Fact]
    public async Task Staff_caller_cannot_list_rows_it_captured_under_the_old_rule_403()
    {
        var (staffToken, staffId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(staffId, "Staff", "Gate Officer", "موظف");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "OLDRULEBADGE", "Harvested", "زائر");
        await SeedLegacyCaptureAsync(staffId, visitorId);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", staffToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    private async Task<Notification> SingleCaptureNotificationAsync(Guid visitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await identityDb.Notifications
            .AsNoTracking()
            .SingleAsync(n => n.UserId == visitorId
                && n.Kind == NotificationKind.ExhibitorLeadCaptured);
    }

    // -- seeding ---------------------------------------------------------------

    private Task SeedExhibitorProfileAsync(Guid userId, string name, string nameArabic) =>
        // Reuse the seeder's canonical "Exhibitor" profile type — D-611 added a
        // unique index on the active ProfileType.Name (the admin service already
        // returns 409 for the same duplicate), so seeding a second "Exhibitor"
        // row now collides. It carries MobileAppRole.Exhibitor (D-519), which is
        // what the scan authorises on (DEF-EXH-001).
        SeedProfileAsync(userId, "Exhibitor", name, nameArabic);

    // DEF-EXH-009 — a scanned visitor now carries the seeded audience type
    // ("Normal", IsForVisitor=true) instead of no profile type at all, so both
    // directions of the eligibility branch are covered by real data.
    private Task SeedVisitorWithQrAsync(
        Guid userId, string qrId, string name, string nameArabic) =>
        SeedProfileAsync(userId, "Normal", name, nameArabic, qrId);

    private async Task SeedProfileAsync(
        Guid userId, string profileTypeName, string name, string nameArabic,
        string? qrId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await appDb.ProfileTypes
            .FirstAsync(profileType => profileType.Name == profileTypeName
                && profileType.IsActive);
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            ProfileTypeId = type.Id,
            QrId = qrId,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    // A capture written straight to the table, exactly as one taken before the
    // subject test existed — no eligibility check of any kind at write time.
    private async Task SeedLegacyCaptureAsync(Guid exhibitorUserId, Guid visitorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.ExhibitorVisitorScans.Add(new ExhibitorVisitorScan
        {
            Id = Guid.NewGuid(),
            ExhibitorUserId = exhibitorUserId,
            VisitorUserId = visitorUserId,
            Note = "captured under the old rule",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<Guid> SeedExhibitorCompanyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = $"Booth {Guid.NewGuid():N}"[..16],
            NameArabic = "جناح",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Exhibitors.Add(exhibitor);
        await appDb.SaveChangesAsync();
        return exhibitor.Id;
    }

    private async Task<MobileAppRole?> AssignedMobileAppRoleAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.ProfileType != null)
            .Select(profile => (MobileAppRole?)profile.ProfileType!.MobileAppRole)
            .FirstOrDefaultAsync();
    }

    /// <summary>Completes a CP-provisioned account the way the invite + approval
    /// flow does (password set out-of-band, desk approves) and signs it in on the
    /// app audience, so the returned token is a real booth-officer token.</summary>
    private async Task<string> SignInProvisionedAccountAsync(string email)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = await users.FindByEmailAsync(email);
            await users.AddPasswordAsync(user!, AuthFlow.Password);
        }
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        const string administratorRole = "Administrator";
        var email = $"exhibitor-scan-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(administratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = administratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Exhibitor Scan Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, administratorRole);
        }

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task DeactivateProfileAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.FirstAsync(p => p.UserId == userId);
        profile.Deactivate();
        await appDb.SaveChangesAsync();
    }

    private async Task<(string AccessToken, Guid UserId)> CreateApprovedUserAsync()
    {
        var email = $"ex-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, UserIdFromToken(token));
    }

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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
