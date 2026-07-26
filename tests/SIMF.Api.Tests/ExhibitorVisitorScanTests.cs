// D-426 — exhibitor lead capture: scan a visitor badge → capture to My Visitors
// + return the full card.
// DEF-EXH-001 — only a profile type carrying MobileAppRole.Exhibitor may scan
//               (Staff / Moderator / plain Visitor are all 403).
// DEF-EXH-003 — the scanned SUBJECT must be an active audience-side account;
//               a partner badge answers the same 404 as an unknown code.
// DEF-EXH-002 — a NEW capture notifies the visitor once, naming the exhibitor;
//               an idempotent re-scan raises nothing.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
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
