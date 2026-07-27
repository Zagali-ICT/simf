// D-426 — exhibitor lead capture: scan a visitor badge → capture to My Visitors
// + return the full card.
// DEF-EXH-001 — only a profile type carrying MobileAppRole.Exhibitor may scan
//               (Staff / Moderator / plain Visitor are all 403).
// D-780 (owner decision 2026-07-27, "can scan all badges") — ALL badges are
//               scannable: media, sponsor, staff and fellow-exhibitor badges are
//               capturable leads. Only a DEACTIVATED account is refused, with the
//               same 404 as an unknown code. This REVERSES the premise of
//               DEF-EXH-003, which had narrowed the subject to audience-side types.
// D-781 (owner decision 2026-07-27) — an EXISTING account can be attached to an
//               exhibitor from the CP, so an exhibitor-typed account created
//               through the generic Others pipeline is no longer locked out of the
//               booth tools by the DEF-EXH-006 membership rule.
// DEF-EXH-002 — a NEW capture notifies the visitor once, naming the exhibitor;
//               an idempotent re-scan raises nothing.
// DEF-EXH-004 — the subject test also runs on the READ path, so a row captured
//               while the old rule was in force stops projecting a card.
// DEF-EXH-005 — a booth officer provisioned from the CP can actually scan.
// DEF-EXH-006 — the role is not authority on its own: revoking the booth
//               membership (or closing the exhibitor) refuses scan AND list.
// DEF-EXH-007 — the capture notice names the EXHIBITOR the officer represents,
//               which a CP-provisioned stub profile could never supply.
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

    // D-780 (owner decision 2026-07-27, "can scan all badges") — a MEDIA badge, a
    // SPONSOR badge, a STAFF badge and a fellow exhibitor's badge are all
    // capturable leads. Before the decision each of these answered 404 (the
    // DEF-EXH-003 audience-side-only rule), so this fails without the change.
    [Fact]
    public async Task Every_active_badge_is_capturable_whatever_its_profile_type()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Booth", "جناح أكمي");

        var (_, mediaId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(mediaId, "Media", "Press One", "صحفي", "MEDIABADGE1");

        var (_, sponsorId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(sponsorId, "Sponsor", "Sponsor One", "راعٍ", "SPONSORBADGE1");

        var (_, staffId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(staffId, "Staff", "Gate Officer", "موظف", "STAFFBADGE1");

        var (_, rivalId) = await CreateApprovedUserAsync();
        await SeedProfileAsync(
            rivalId, "Exhibitor", "Rival Booth", "جناح منافس", "RIVALBADGE1");

        foreach (var badge in new[]
                 { "MEDIABADGE1", "SPONSORBADGE1", "STAFFBADGE1", "RIVALBADGE1" })
        {
            var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
                new ScanVisitorBadgeRequest { QrId = badge }, exhibitorToken);
            Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        }

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitorToken);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, row => row.Card.UserId == mediaId);
        Assert.Contains(rows, row => row.Card.UserId == sponsorId);
        Assert.Contains(rows, row => row.Card.UserId == staffId);
        Assert.Contains(rows, row => row.Card.UserId == rivalId);
    }

    // D-780 keeps the IsActive half of the subject rule: a deactivated
    // (soft-deleted) account is not a valid attendee, and it answers the SAME 404
    // as an unknown code so the scan never leaks that the badge exists.
    [Fact]
    public async Task Deactivated_badge_subject_returns_404()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Booth", "جناح أكمي");

        var (_, retiredId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(retiredId, "RETIREDBADGE", "Retired", "متقاعد");
        await DeactivateProfileAsync(retiredId);

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "RETIREDBADGE" }, exhibitorToken);
        Assert.Equal(HttpStatusCode.NotFound, scan.StatusCode);

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

    // DEF-EXH-004 — the READ path re-runs the capture-time subject test, so a row
    // captured while there was no subject test at all stops projecting a live card
    // for a since-DEACTIVATED subject. D-780 widened the rule itself, so the STAFF
    // capture here is now a legitimate lead and DOES list.
    [Fact]
    public async Task Legacy_captures_of_deactivated_subjects_are_not_listed()
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

        // Both ACTIVE subjects survive (D-780 — a staff badge is a lead like any
        // other); no PII (email or either mobile number) is projected for the
        // deactivated one.
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Card.UserId == keptId);
        Assert.Contains(rows, row => row.Card.UserId == staffId);
        Assert.DoesNotContain(rows, row => row.Card.UserId == retiredId);
    }

    // DEF-EXH-006 — scan authority must follow a CURRENT booth membership, not
    // only the profile type. The officer's ProfileType (and the app role baked
    // into their existing token) survives the revocation, so only a server-side
    // membership test can stop them; the 200 taken while the membership was live
    // proves the rule does not simply lock every officer out.
    [Fact]
    public async Task Booth_officer_is_refused_once_the_membership_is_revoked()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync();
        var (officerToken, officerUserId) =
            await ProvisionBoothOfficerAsync(adminToken, exhibitorId, "Revoked Officer");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "REVOKEBADGE", "Lead Two", "زائر");

        // While the membership is live the officer works normally.
        var allowed = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "REVOKEBADGE" }, officerToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        await RevokeMembershipAsync(officerUserId);

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "REVOKEBADGE" }, officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // DEF-EXH-006 — the CP's own revocation path. Closing the exhibitor
    // (DELETE /admin/exhibitors/{id}, a soft-delete) ends every officer's booth
    // membership, so none of them may keep harvesting visitor cards.
    [Fact]
    public async Task Closing_the_exhibitor_revokes_its_officers_scan_authority()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync();
        var (officerToken, _) =
            await ProvisionBoothOfficerAsync(adminToken, exhibitorId, "Closed Booth Officer");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "CLOSEDBOOTH", "Lead Three", "زائر");

        var closed = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}", adminToken);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "CLOSEDBOOTH" }, officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // DEF-EXH-007 — the whole point of the capture notice is that it NAMES who
    // received the data. A CP-provisioned officer's UserProfile is a stub with no
    // Name / NameArabic at all (AdminAccountService.CreateAccountAsync), so taking
    // the name off the scanning account degraded the notice to "An exhibitor" for
    // exactly the accounts the CP creates. It must name the EXHIBITOR they
    // represent, in both languages.
    [Fact]
    public async Task Capture_notice_names_the_exhibitor_a_cp_officer_represents()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync(
            "Northern Shipyards", "أحواض الشمال");
        var (officerToken, _) =
            await ProvisionBoothOfficerAsync(adminToken, exhibitorId, "Named Officer");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "NAMEDBADGE", "Visitor Three", "الزائر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "NAMEDBADGE" }, officerToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        var notification = await SingleCaptureNotificationAsync(visitorId);
        Assert.Contains("Northern Shipyards", notification.Body);
        Assert.Contains("أحواض الشمال", notification.BodyArabic);
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

    // D-781 — the Others-pipeline lockout. An exhibitor-typed account created
    // through POST /admin/others gets the right profile type but NO
    // ExhibitorMembership, and DEF-EXH-006 made a current membership half the
    // authorisation — so it was 403 on scan AND on My Visitors with no CP path to
    // attach it to a booth. Linking it from the CP is that path. Fails without the
    // link endpoint: the account can never reach 200.
    [Fact]
    public async Task Others_pipeline_account_can_scan_once_it_is_linked_to_an_exhibitor()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync("Linked Booth", "جناح مرتبط");

        var (officerEmail, officerToken) =
            await CreateOtherAccountAsync(adminToken, "Exhibitor", "Others Desk Officer");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "LINKEDBADGE1", "Lead Four", "زائر");

        // Locked out before the link — the profile type alone is not authority.
        var beforeScan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "LINKEDBADGE1" }, officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, beforeScan.StatusCode);
        var beforeList = await GetAuthAsync("/api/v1/app/exhibitor/visitors", officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, beforeList.StatusCode);

        var link = await LinkAccountAsync(adminToken, exhibitorId, officerEmail, "Stand lead");
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var account = (await link.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Data!;
        Assert.Equal(officerEmail, account.Email);
        Assert.Equal("Stand lead", account.RoleLabel);

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "LINKEDBADGE1" }, officerToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", officerToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Equal("Lead Four", Assert.Single(rows).Card.Name);

        // The linked account shows up under the exhibitor's accounts in the CP.
        var accounts = await GetAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts", adminToken);
        var summaries = (await accounts.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorAccountSummary>>>())!.Data!;
        Assert.Contains(summaries, summary => summary.Email == officerEmail);
    }

    // D-781 — linking is not a back door onto the booth tools: the target account
    // must already carry an exhibitor-mapped profile type. A Media account is
    // refused with a distinct 409 rather than silently gaining scan authority.
    [Fact]
    public async Task Linking_refuses_an_account_that_is_not_exhibitor_typed()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync();

        var (mediaEmail, _) =
            await CreateOtherAccountAsync(adminToken, "Media", "Press Desk");

        var link = await LinkAccountAsync(adminToken, exhibitorId, mediaEmail, null);
        Assert.Equal(HttpStatusCode.Conflict, link.StatusCode);
        var error = (await link.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Error!;
        Assert.Equal(ErrorCodes.ExhibitorAccountNotEligible, error.Code);
    }

    // D-781 — an unknown email is a clean 404, and an account already holding an
    // active membership is a clean 409 (the filtered unique index on
    // ExhibitorMembership.UserId is a backstop, not the error surface).
    [Fact]
    public async Task Linking_refuses_an_unknown_email_and_an_already_linked_account()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorCompanyAsync();
        var otherExhibitorId = await SeedExhibitorCompanyAsync();

        var unknown = await LinkAccountAsync(
            adminToken, exhibitorId, $"nobody-{Guid.NewGuid():N}@simf.test", null);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var notFound = (await unknown.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Error!;
        Assert.Equal(ErrorCodes.ExhibitorAccountNotFound, notFound.Code);

        var (officerEmail, _) =
            await CreateOtherAccountAsync(adminToken, "Exhibitor", "Double Linked");
        Assert.Equal(HttpStatusCode.OK,
            (await LinkAccountAsync(adminToken, exhibitorId, officerEmail, null)).StatusCode);

        var second = await LinkAccountAsync(adminToken, otherExhibitorId, officerEmail, null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var conflict = (await second.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Error!;
        Assert.Equal(ErrorCodes.ExhibitorAccountAlreadyLinked, conflict.Code);
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

    private async Task SeedExhibitorProfileAsync(Guid userId, string name, string nameArabic)
    {
        // Reuse the seeder's canonical "Exhibitor" profile type — D-611 added a
        // unique index on the active ProfileType.Name (the admin service already
        // returns 409 for the same duplicate), so seeding a second "Exhibitor"
        // row now collides. It carries MobileAppRole.Exhibitor (D-519), which is
        // what the scan authorises on (DEF-EXH-001).
        await SeedProfileAsync(userId, "Exhibitor", name, nameArabic);

        // DEF-EXH-006 — a booth officer is an officer OF a booth: the role alone
        // no longer authorises the tools, so give them the exhibitor they belong
        // to. It carries the same display name, which is what the capture notice
        // now names (DEF-EXH-007).
        var exhibitorId = await SeedExhibitorCompanyAsync(name, nameArabic);
        await SeedMembershipAsync(exhibitorId, userId, name);
    }

    private async Task SeedMembershipAsync(
        Guid exhibitorId, Guid userId, string contactName)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.ExhibitorMemberships.Add(new ExhibitorMembership
        {
            Id = Guid.NewGuid(),
            ExhibitorId = exhibitorId,
            UserId = userId,
            ContactName = contactName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

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

    private async Task<Guid> SeedExhibitorCompanyAsync(
        string? name = null, string? nameArabic = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Booth {Guid.NewGuid():N}"[..16],
            NameArabic = nameArabic ?? "جناح",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Exhibitors.Add(exhibitor);
        await appDb.SaveChangesAsync();
        return exhibitor.Id;
    }

    /// <summary>The exhibitor the officer represents drops them from the booth —
    /// the ExhibitorMembership soft-delete the entity documents. The officer's
    /// ProfileType, account state and already-issued token are all untouched.</summary>
    private async Task RevokeMembershipAsync(Guid officerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var membership = await appDb.ExhibitorMemberships
            .FirstAsync(m => m.UserId == officerUserId && m.IsActive);
        membership.IsActive = false;
        await appDb.SaveChangesAsync();
    }

    /// <summary>Runs the CP's real provisioning path (POST
    /// /admin/exhibitors/{id}/accounts) and returns a signed-in booth-officer
    /// token plus the account's user id.</summary>
    private async Task<(string AccessToken, Guid UserId)> ProvisionBoothOfficerAsync(
        string adminToken, Guid exhibitorId, string contactName)
    {
        var email = $"booth-{Guid.NewGuid():N}@simf.test";
        var provision = await PostAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts",
            new ProvisionExhibitorAccountRequest
            {
                ContactName = contactName,
                Email = email,
                RoleLabel = "Stand lead",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, provision.StatusCode);
        var account = (await provision.Content
            .ReadFromJsonAsync<ApiResult<ExhibitorAccountSummary>>())!.Data!;
        return (await SignInProvisionedAccountAsync(email), account.UserId);
    }

    /// <summary>D-781 — creates an account through the GENERIC Others pipeline
    /// (POST /admin/others), which assigns the partner profile type but writes NO
    /// ExhibitorMembership, then signs it in. Returns its email + app token.</summary>
    private async Task<(string Email, string AccessToken)> CreateOtherAccountAsync(
        string adminToken, string profileTypeName, string displayName)
    {
        var email = $"other-{Guid.NewGuid():N}@simf.test";
        var created = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = displayName,
                ProfileTypeId = await ProfileTypeIdAsync(profileTypeName),
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return (email, await SignInProvisionedAccountAsync(email));
    }

    private Task<HttpResponseMessage> LinkAccountAsync(
        string adminToken, Guid exhibitorId, string email, string? roleLabel) =>
        PostAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/link",
            new LinkExhibitorAccountRequest { Email = email, RoleLabel = roleLabel },
            adminToken);

    private async Task<Guid> ProfileTypeIdAsync(string profileTypeName)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.ProfileTypes
            .AsNoTracking()
            .Where(profileType => profileType.Name == profileTypeName && profileType.IsActive)
            .Select(profileType => profileType.Id)
            .FirstAsync();
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
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
