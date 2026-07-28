// FR-EXH-002 — a captured lead could neither be REMOVED nor EXPORTED. My Contacts
//               has had both a soft-delete and a vCard export since D-286; the
//               exhibitor lead list had neither, so a mis-scan (or a lead the
//               visitor asked to be dropped) was permanent and the card could
//               only be read on screen.
// FR-EXH-003 — a lead belonged to the PERSON who scanned it, not to the booth:
//               ExhibitorVisitorScan carried no ExhibitorId, so two officers of
//               the same exhibitor kept two disjoint lead lists and neither saw
//               the other's captures.
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
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExhibitorLeadManagementTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorLeadManagementTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // FR-EXH-003 — the headline: two officers, ONE booth, ONE shared lead list.
    // Before the ExhibitorId column each officer only ever saw their own captures.
    [Fact]
    public async Task Both_officers_of_a_booth_see_the_same_captured_leads()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("Shared Booth", "جناح مشترك");
        var (firstOfficer, _) = await ProvisionOfficerAsync(admin, boothId, "Officer One");
        var (secondOfficer, _) = await ProvisionOfficerAsync(admin, boothId, "Officer Two");

        await SeedVisitorAsync("SHAREDLEAD1", "Lead Alpha", "زائر ألفا");
        await SeedVisitorAsync("SHAREDLEAD2", "Lead Beta", "زائر بيتا");

        await ScanAsync(firstOfficer, "SHAREDLEAD1", "met at the stand");
        await ScanAsync(secondOfficer, "SHAREDLEAD2", null);

        var seenByFirst = await ListAsync(firstOfficer);
        var seenBySecond = await ListAsync(secondOfficer);

        Assert.Equal(2, seenByFirst.Count);
        Assert.Equal(2, seenBySecond.Count);
        Assert.Equal(
            seenByFirst.Select(r => r.Id).OrderBy(id => id),
            seenBySecond.Select(r => r.Id).OrderBy(id => id));
        Assert.Contains(seenByFirst, r => r.Card.Name == "Lead Alpha");
        Assert.Contains(seenByFirst, r => r.Card.Name == "Lead Beta");
    }

    // FR-EXH-003 — the scope is the booth, not "every exhibitor": a rival booth's
    // officer must never see these leads.
    [Fact]
    public async Task A_rival_booths_officer_does_not_see_the_leads()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var mine = await SeedExhibitorCompanyAsync("Our Booth", "جناحنا");
        var theirs = await SeedExhibitorCompanyAsync("Rival Booth", "جناح منافس");
        var (ourOfficer, _) = await ProvisionOfficerAsync(admin, mine, "Ours");
        var (rivalOfficer, _) = await ProvisionOfficerAsync(admin, theirs, "Theirs");

        await SeedVisitorAsync("RIVALSCOPE1", "Private Lead", "زائر خاص");
        await ScanAsync(ourOfficer, "RIVALSCOPE1", null);

        Assert.Single(await ListAsync(ourOfficer));
        Assert.Empty(await ListAsync(rivalOfficer));
    }

    // FR-EXH-003 — a colleague re-scanning the same visitor updates the booth's
    // ONE lead rather than forking a private second copy of it, and the visitor is
    // notified only for the first capture.
    [Fact]
    public async Task A_colleague_rescanning_updates_the_booths_single_lead()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("One Lead Booth", "جناح واحد");
        var (first, _) = await ProvisionOfficerAsync(admin, boothId, "First");
        var (second, _) = await ProvisionOfficerAsync(admin, boothId, "Second");

        var visitorId = await SeedVisitorAsync("DEDUPEBADGE", "Same Lead", "نفس الزائر");
        await ScanAsync(first, "DEDUPEBADGE", "morning");
        await ScanAsync(second, "DEDUPEBADGE", "afternoon");

        var rows = await ListAsync(first);
        var only = Assert.Single(rows);
        Assert.Equal("afternoon", only.Note);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.Equal(1, await identityDb.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == visitorId
                && n.Kind == NotificationKind.ExhibitorLeadCaptured));
    }

    // FR-EXH-002 — remove. Soft-delete (the project convention), booth-wide, and
    // idempotent so a double tap is not an error.
    [Fact]
    public async Task Removing_a_lead_soft_deletes_it_for_the_whole_booth()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("Remove Booth", "جناح الحذف");
        var (owner, _) = await ProvisionOfficerAsync(admin, boothId, "Owner");
        var (colleague, _) = await ProvisionOfficerAsync(admin, boothId, "Colleague");

        await SeedVisitorAsync("REMOVEBADGE", "Dropped Lead", "زائر محذوف");
        await ScanAsync(owner, "REMOVEBADGE", null);
        var captureId = Assert.Single(await ListAsync(owner)).Id;

        var removed = await DeleteAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}", colleague);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        Assert.Empty(await ListAsync(owner));
        Assert.Empty(await ListAsync(colleague));

        // Soft, not hard — the capture record (and the consent trail behind it)
        // survives with IsActive = false.
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var stored = await appDb.ExhibitorVisitorScans.AsNoTracking()
                .SingleAsync(s => s.Id == captureId);
            Assert.False(stored.IsActive);
            Assert.Equal(boothId, stored.ExhibitorId);
        }

        // Idempotent — removing it again is still a 200.
        var again = await DeleteAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}", owner);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // FR-EXH-002 — a rival booth cannot delete our leads (the delete honours the
    // same booth scope as the list, and silently no-ops rather than confirming the
    // capture exists).
    [Fact]
    public async Task A_rival_booth_cannot_remove_our_lead()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var mine = await SeedExhibitorCompanyAsync("Protected Booth", "جناح محمي");
        var theirs = await SeedExhibitorCompanyAsync("Other Booth", "جناح آخر");
        var (ourOfficer, _) = await ProvisionOfficerAsync(admin, mine, "Keeper");
        var (rivalOfficer, _) = await ProvisionOfficerAsync(admin, theirs, "Intruder");

        await SeedVisitorAsync("PROTECTBADGE", "Kept Lead", "زائر محفوظ");
        await ScanAsync(ourOfficer, "PROTECTBADGE", null);
        var captureId = Assert.Single(await ListAsync(ourOfficer)).Id;

        await DeleteAuthAsync($"/api/v1/app/exhibitor/visitors/{captureId}", rivalOfficer);

        Assert.Single(await ListAsync(ourOfficer));
    }

    // FR-EXH-002 — vCard export, mirroring GET /app/contacts/{id}/vcard exactly.
    [Fact]
    public async Task Exports_a_captured_lead_as_a_vcard()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("Export Booth", "جناح التصدير");
        var (officer, _) = await ProvisionOfficerAsync(admin, boothId, "Exporter");

        await SeedVisitorAsync(
            "VCARDBADGE", "Nabil Farid", "نبيل فريد",
            jobTitle: "Fleet Engineer", saudiMobile: "0501234567");
        await ScanAsync(officer, "VCARDBADGE", null);
        var captureId = Assert.Single(await ListAsync(officer)).Id;

        var export = await GetAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}/vcard", officer);

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("text/vcard", export.Content.Headers.ContentType!.MediaType);
        var vcf = await export.Content.ReadAsStringAsync();
        Assert.StartsWith("BEGIN:VCARD\r\nVERSION:3.0\r\n", vcf, StringComparison.Ordinal);
        Assert.Contains("FN:Nabil Farid", vcf, StringComparison.Ordinal);
        Assert.Contains("TITLE:Fleet Engineer", vcf, StringComparison.Ordinal);
        Assert.Contains("TEL;TYPE=CELL:0501234567", vcf, StringComparison.Ordinal);
        Assert.EndsWith("END:VCARD\r\n", vcf, StringComparison.Ordinal);
    }

    // FR-EXH-002 — the export is not a second door onto a card the list refuses:
    // a lead from another booth 404s rather than rendering.
    [Fact]
    public async Task Cannot_export_a_lead_from_another_booth()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var mine = await SeedExhibitorCompanyAsync("Owner Booth", "جناح المالك");
        var theirs = await SeedExhibitorCompanyAsync("Stranger Booth", "جناح غريب");
        var (ourOfficer, _) = await ProvisionOfficerAsync(admin, mine, "Owner");
        var (rivalOfficer, _) = await ProvisionOfficerAsync(admin, theirs, "Stranger");

        await SeedVisitorAsync("NOEXPORTBADGE", "Guarded Lead", "زائر محمي");
        await ScanAsync(ourOfficer, "NOEXPORTBADGE", null);
        var captureId = Assert.Single(await ListAsync(ourOfficer)).Id;

        var export = await GetAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}/vcard", rivalOfficer);

        Assert.Equal(HttpStatusCode.NotFound, export.StatusCode);
    }

    // FR-EXH-002 — both new actions carry the same gate as the rest of the booth
    // tools: a plain visitor token is refused outright (DEF-EXH-001 / DEF-EXH-006).
    [Fact]
    public async Task A_visitor_token_cannot_remove_or_export_a_lead()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("Gated Booth", "جناح مقيّد");
        var (officer, _) = await ProvisionOfficerAsync(admin, boothId, "Gate Keeper");

        await SeedVisitorAsync("GATEDBADGE", "Gated Lead", "زائر مقيّد");
        await ScanAsync(officer, "GATEDBADGE", null);
        var captureId = Assert.Single(await ListAsync(officer)).Id;

        var visitor = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(
            _client, _factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}", visitor.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAuthAsync(
            $"/api/v1/app/exhibitor/visitors/{captureId}/vcard",
            visitor.AccessToken)).StatusCode);
        Assert.Single(await ListAsync(officer));
    }

    // FR-EXH-003 — a row written before the column existed (the migration's
    // "no membership to resolve" case) stays visible to its capturer, and the first
    // re-scan adopts it into that officer's booth so the booth list picks it up.
    [Fact]
    public async Task A_legacy_untagged_capture_stays_visible_and_is_adopted_on_rescan()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var boothId = await SeedExhibitorCompanyAsync("Legacy Booth", "جناح قديم");
        var (officer, officerId) = await ProvisionOfficerAsync(admin, boothId, "Legacy");
        var (colleague, _) = await ProvisionOfficerAsync(admin, boothId, "Newcomer");

        var visitorId = await SeedVisitorAsync("LEGACYTAG", "Legacy Lead", "زائر قديم");
        await SeedUntaggedCaptureAsync(officerId, visitorId);

        // Visible to its capturer; not yet to the colleague (it carries no booth).
        Assert.Single(await ListAsync(officer));
        Assert.Empty(await ListAsync(colleague));

        await ScanAsync(officer, "LEGACYTAG", "adopted");

        // Still ONE row — adopted, not duplicated — and now the booth's.
        var afterFirst = Assert.Single(await ListAsync(officer));
        Assert.Equal("adopted", afterFirst.Note);
        Assert.Equal(afterFirst.Id, Assert.Single(await ListAsync(colleague)).Id);
    }

    // FR-EXH-003 — the uniqueness invariant moved with the ownership model. A lead
    // is now one per (BOOTH, visitor), so an officer who transfers to another booth
    // and re-scans a visitor they already captured for their old one must capture a
    // SECOND, separate lead — the old booth keeps its own. The superseded
    // (officer, visitor) unique index would have rejected that insert outright.
    [Fact]
    public async Task An_officer_who_moves_booths_captures_a_second_lead_for_the_new_one()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var oldBooth = await SeedExhibitorCompanyAsync("Former Booth", "الجناح السابق");
        var newBooth = await SeedExhibitorCompanyAsync("Current Booth", "الجناح الحالي");
        var (atOldBooth, officerId) = await ProvisionOfficerAsync(admin, oldBooth, "Mover");
        var (colleagueAtOld, _) = await ProvisionOfficerAsync(admin, oldBooth, "Stayer");

        await SeedVisitorAsync("MOVERBADGE", "Followed Lead", "زائر متابع");
        await ScanAsync(atOldBooth, "MOVERBADGE", "captured for the old booth");

        var atNewBooth = await MoveOfficerToBoothAsync(officerId, newBooth);
        await ScanAsync(atNewBooth, "MOVERBADGE", "captured for the new booth");

        // The old booth keeps its lead — losing it when an officer resigns would
        // silently delete the company's own record of a visitor it met.
        var kept = Assert.Single(await ListAsync(colleagueAtOld));
        Assert.Equal("captured for the old booth", kept.Note);

        // …and the new booth has its own, separate one.
        var captured = Assert.Single(await ListAsync(atNewBooth));
        Assert.Equal("captured for the new booth", captured.Note);
        Assert.NotEqual(kept.Id, captured.Id);
    }

    // -- helpers ---------------------------------------------------------------

    /// <summary>Deactivates the officer's current membership and grants them one on
    /// [exhibitorId] instead (only ONE active membership per user is allowed), then
    /// signs them back in so the new booth is on their token's read path.</summary>
    private async Task<string> MoveOfficerToBoothAsync(Guid officerUserId, Guid exhibitorId)
    {
        string email;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var current = await appDb.ExhibitorMemberships
                .Where(m => m.UserId == officerUserId && m.IsActive)
                .ToListAsync();
            foreach (var membership in current)
            {
                membership.Deactivate();
            }
            appDb.ExhibitorMemberships.Add(new ExhibitorMembership
            {
                Id = Guid.NewGuid(),
                ExhibitorId = exhibitorId,
                UserId = officerUserId,
                ContactName = "Mover",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            email = (await users.FindByIdAsync(officerUserId.ToString()))!.Email!;
        }

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task<IReadOnlyList<ExhibitorVisitorRow>> ListAsync(string token)
    {
        var response = await GetAuthAsync("/api/v1/app/exhibitor/visitors", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
    }

    private async Task ScanAsync(string token, string qrId, string? note)
    {
        var response = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = qrId, Note = note }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A capture written straight to the table with NO ExhibitorId — a row
    /// exactly as the migration leaves one whose capturer had no membership to
    /// backfill from.</summary>
    private async Task SeedUntaggedCaptureAsync(Guid exhibitorUserId, Guid visitorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.ExhibitorVisitorScans.Add(new ExhibitorVisitorScan
        {
            Id = Guid.NewGuid(),
            ExhibitorUserId = exhibitorUserId,
            ExhibitorId = null,
            VisitorUserId = visitorUserId,
            Note = "captured before the booth column existed",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<Guid> SeedVisitorAsync(
        string qrId, string name, string nameArabic,
        string? jobTitle = null, string? saudiMobile = null)
    {
        var (_, userId) = await CreateApprovedUserAsync();
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await appDb.ProfileTypes
            .FirstAsync(profileType => profileType.Name == "Normal" && profileType.IsActive);
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            JobTitle = jobTitle,
            SaudiMobile = saudiMobile,
            ProfileTypeId = type.Id,
            QrId = qrId,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> SeedExhibitorCompanyAsync(string name, string nameArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = $"{name} {Guid.NewGuid():N}"[..24],
            NameArabic = nameArabic,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Exhibitors.Add(exhibitor);
        await appDb.SaveChangesAsync();
        return exhibitor.Id;
    }

    /// <summary>Runs the CP's real provisioning path so the officer comes out with
    /// the exhibitor profile type AND a live booth membership (DEF-EXH-005/006).</summary>
    private async Task<(string AccessToken, Guid UserId)> ProvisionOfficerAsync(
        string adminToken, Guid exhibitorId, string contactName)
    {
        var email = $"lead-{Guid.NewGuid():N}@simf.test";
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
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, account.UserId);
    }

    private async Task<(string AccessToken, Guid UserId)> CreateApprovedUserAsync()
    {
        var email = $"lead-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(
                    _factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, UserIdFromToken(token));
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"lead-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Lead Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
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

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(bytes));
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
