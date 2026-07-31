// BF-02 end-to-end journey: walk-in desk -> pending queue -> approve -> QR ->
// موج (Mawj) VIP roster.
//
// Every leg of this chain already has a test. What none of them has is the
// SEAM. WalkInRegistrationTests proves a fresh walk-in carries no QR;
// AdminApprovalTests proves an approve mints one and applies an optional tier;
// VipRosterTests proves the roster renders VVIP rows. Each starts from its own
// freshly-seeded subject, so nothing has ever asserted that the profile row the
// desk creates is the same row approval mints a QR onto, and the same row the
// موج teams then read off the roster.
//
// That seam is where D-386 and D-425 actually live: "no badge until approval"
// is a claim about ONE account moving through three surfaces, not about three
// endpoints each behaving well in isolation. A regression that made approval
// mint the QR onto a second, orphaned profile row — or that left the roster
// reading a stale tier — would pass every existing test in this suite.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class Journey02RegisterApproveRosterTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";

    // The Crockford base32 alphabet QrIdMinter draws from (D-046) — no I, L, O,
    // U, 0 or 1, so a hand-printed badge id cannot be mis-keyed at a gate.
    private const string CrockfordAlphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int QrIdLength = 12;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public Journey02RegisterApproveRosterTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>
    /// E2E-BF-02-001 + -002 + -009 as one continuous journey. A VVIP is
    /// registered at the desk, sits in the pending queue with no badge, is
    /// approved with a confirmed tier, and surfaces on the موج roster — with
    /// every step keyed to the user id the FIRST call returned.
    /// </summary>
    [Fact]
    public async Task A_vip_walk_in_carries_no_qr_until_approval_then_the_same_row_reaches_the_roster()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var vvipTierId = await AudienceProfileTypeIdAsync("VVIP");
        var organisationId = await OrganisationIdAsync();

        // The desk-typed موج id is the payload that has to survive the whole
        // journey. It is the only field an operator enters at registration that
        // the roster reads back, so it doubles as an end-to-end tracer: if the
        // roster row carried a different profile, this would not match.
        var mawjId = $"MAWJ-{Guid.NewGuid():N}"[..16];
        var email = $"bf02-vvip-{Guid.NewGuid():N}@simf.test";

        // ---- Step 1: the VIP desk registers the visitor (D-425) -------------
        var request = BuildWalkInRequest(vvipTierId, email, organisationId);
        request.MawjId = mawjId;
        request.Honorific = "His Excellency";
        request.PreferredLanguage = "ar";

        var registration = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", request, adminToken);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var registered = (await registration.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.True(registered.Success);

        var subjectId = registered.Data!.UserId;
        Assert.NotEqual(Guid.Empty, subjectId);
        // D-425 — the success modal keys off an EMPTY QrId to decide it must not
        // offer a badge to print. The desk hands out nothing at this point.
        Assert.Equal(string.Empty, registered.Data.QrId);

        // The profile row id is captured here and re-asserted after approval:
        // it is what proves approval mutated THIS row rather than minting a QR
        // onto a second profile for the same user.
        Guid profileRowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var user = await identityDb.Users.SingleAsync(u => u.Id == subjectId);
            Assert.Equal(AccountState.PendingApproval, user.AccountState);
            Assert.Equal(UserType.Visitor, user.UserType);

            var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
            profileRowId = profile.Id;
            Assert.True(
                string.IsNullOrEmpty(profile.QrId),
                "A pending walk-in already had a QR id before any approval (D-425/D-386).");
            Assert.Equal(vvipTierId, profile.ProfileTypeId);
            Assert.Equal(mawjId, profile.MawjId);
        }

        // ---- Step 2: the pending queue is holding THIS account --------------
        // Matching on the id alone would pass against any pending row, so the
        // email is checked on the same row: the queue entry is this visitor.
        var pendingBefore = await ListPendingVisitorsAsync(adminToken);
        var queued = Assert.Single(pendingBefore, row => row.Id == subjectId);
        Assert.Equal(email, queued.Email);

        // ---- Step 3: the roster already lists them, marked NOT approved -----
        // The roster is scoped by TIER, not by account state — it exposes the
        // state as a column instead. Asserting the pre-approval value here is
        // what makes the post-approval assertion below a transition rather than
        // a snapshot that might always have read "Approved".
        var rosterBefore = await GetRosterAsync(adminToken);
        var rowBefore = Assert.Single(rosterBefore, row => row.UserId == subjectId);
        Assert.Equal(mawjId, rowBefore.MawjId);
        Assert.Equal("VVIP", rowBefore.TierName);
        Assert.Equal(nameof(AccountState.PendingApproval), rowBefore.AccountState);
        // The roster falls back to "PendingApproval" when its cross-DB identity
        // lookup finds nothing, so a resolved email is what distinguishes a real
        // pending state from an unresolved row that merely reads like one.
        Assert.Equal(email, rowBefore.Email);

        // ---- Step 4: approve, confirming the tier the desk picked (D-386) ---
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveWithTierBody(vvipTierId),
            adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = (await approve.Content.ReadFromJsonAsync<ApiResult<bool>>())!;
        Assert.True(approved.Data);

        // ---- Step 5: the QR appears on the SAME profile row -----------------
        string mintedQrId;
        using (var verifyScope = _factory.Services.CreateScope())
        {
            // A fresh scope: the scope above tracks the pre-approval entities,
            // so re-querying it would hand back the stale instances.
            var identityDb = verifyScope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = verifyScope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var user = await identityDb.Users.SingleAsync(u => u.Id == subjectId);
            Assert.Equal(AccountState.Approved, user.AccountState);

            var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
            Assert.Equal(profileRowId, profile.Id);
            Assert.Equal(vvipTierId, profile.ProfileTypeId);

            mintedQrId = profile.QrId!;
            Assert.False(
                string.IsNullOrEmpty(mintedQrId),
                "Approval reported success but minted no QR id (D-386).");
            // A non-empty string is not yet a usable badge. The gate scanner and
            // the badge renderer both assume the Crockford-12 shape, so assert
            // the value is actually printable/scannable, not merely present.
            Assert.Equal(QrIdLength, mintedQrId.Length);
            Assert.True(
                mintedQrId.All(character => CrockfordAlphabet.Contains(character)),
                $"Minted QR id '{mintedQrId}' contains characters outside the Crockford alphabet.");
        }

        // ---- Step 6: the queue releases the account -------------------------
        var pendingAfter = await ListPendingVisitorsAsync(adminToken);
        Assert.DoesNotContain(pendingAfter, row => row.Id == subjectId);

        // ---- Step 7: the roster row flips to Approved -----------------------
        // Same user id, same موج tracer, same tier — only the state moved. This
        // is the join the journey exists to prove: the row the موج teams export
        // is the row the desk created and the row approval badged.
        var rosterAfter = await GetRosterAsync(adminToken);
        var rowAfter = Assert.Single(rosterAfter, row => row.UserId == subjectId);
        Assert.Equal(nameof(AccountState.Approved), rowAfter.AccountState);
        Assert.Equal(mawjId, rowAfter.MawjId);
        Assert.Equal("VVIP", rowAfter.TierName);
        Assert.Equal("His Excellency", rowAfter.Honorific);
        // Cross-DB (D-157): the email lives on SIMF_Identity and is resolved on
        // read, so this also proves the roster's join to the identity side
        // followed the right user id rather than defaulting to empty.
        Assert.Equal(email, rowAfter.Email);

        // ---- Step 8: the CP export grid agrees with the JSON feed -----------
        // VipExport.razor renders roster/list, not the raw feed. Searching it by
        // the desk-typed موج id proves an operator can actually find the visitor
        // on the page — the two surfaces could diverge, since the paged one
        // applies its own in-memory search/sort layer.
        var gridPage = await PostAuthAsync(
            "/api/v1/admin/visitors/vip/roster/list",
            new GridQuery { Search = mawjId, Top = 50 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, gridPage.StatusCode);
        var grid = (await gridPage.Content
            .ReadFromJsonAsync<ApiResult<GridPage<VipRosterRow>>>())!;
        // Total is the post-filter count, so 1 proves the search actually
        // narrowed rather than the page happening to start with our row.
        Assert.Equal(1, grid.Data!.Total);
        var gridRow = Assert.Single(grid.Data.Items, row => row.UserId == subjectId);
        Assert.Equal(nameof(AccountState.Approved), gridRow.AccountState);
    }

    /// <summary>
    /// The control that makes the journey's roster assertion causal. An ordinary
    /// Normal-tier walk-in, approved by the same call, gets its QR — and stays
    /// off the موج roster.
    ///
    /// <para>Without this, step 7 above could pass for the wrong reason: it
    /// would look identical if the roster simply listed every approved visitor.
    /// Pairing them pins the roster's membership rule to the TIER, which is the
    /// half of D-386 that decides who the موج teams greet at the door.</para>
    /// </summary>
    [Fact]
    public async Task An_approved_normal_walk_in_is_badged_but_never_reaches_the_vip_roster()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var normalTierId = await AudienceProfileTypeIdAsync("Normal");
        var organisationId = await OrganisationIdAsync();
        var email = $"bf02-normal-{Guid.NewGuid():N}@simf.test";

        var registration = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildWalkInRequest(normalTierId, email, organisationId),
            adminToken);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var subjectId = (await registration.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!.UserId;

        // No tier in the body — the desk already set one, and D-386 says null
        // leaves it alone. This is the ordinary approval a clerk performs.
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve",
            new ApproveWithTierBody(null),
            adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var user = await identityDb.Users.SingleAsync(u => u.Id == subjectId);
            Assert.Equal(AccountState.Approved, user.AccountState);

            var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
            // Approval badges EVERY visitor, VIP or not — so the roster absence
            // below cannot be explained away as "this one was never approved".
            Assert.False(string.IsNullOrEmpty(profile.QrId));
            Assert.Equal(normalTierId, profile.ProfileTypeId);
        }

        var roster = await GetRosterAsync(adminToken);
        Assert.DoesNotContain(roster, row => row.UserId == subjectId);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>The approve body (D-386). Null <c>ProfileTypeId</c> means "keep
    /// the tier the desk set"; a value confirms or changes it.</summary>
    private sealed record ApproveWithTierBody(Guid? ProfileTypeId);

    private async Task<IReadOnlyList<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        string adminToken)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/pending/list",
            new GridQuery { Top = 200 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!;
        return page.Data!.Items;
    }

    private async Task<IReadOnlyList<VipRosterRow>> GetRosterAsync(string adminToken)
    {
        var response = await GetAuthAsync("/api/v1/admin/visitors/vip/roster", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roster = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<VipRosterRow>>>())!;
        Assert.True(roster.Success);
        return roster.Data!;
    }

    private static AdminWalkInRegistrationRequest BuildWalkInRequest(
        Guid profileTypeId, string email, Guid organisationId) =>
        new()
        {
            Email = email,
            DisplayName = "BF-02 Journey Subject",
            ArabicName = "زائر رحلة الاختبار",
            EnglishName = "BF-02 Journey Visitor",
            ProfileTypeId = profileTypeId,
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1988, 4, 12),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            // H-1 — the class shares one DB and the desk dedups on the National-ID
            // blind index, so a reused id would 409 the second registration.
            NationalId = TestIdentity.MintNationalId(),
            SaudiMobile = "+966500000001",
            // B3 — D-221: organisation is required at the desk.
            OrganisationId = organisationId,
        };

    /// <summary>Find-or-create the named audience-side tier. The seeder ships
    /// Normal / VVIP / VIP as <c>IsForVisitor=true</c>; the create fallback keeps
    /// the test independent of seed order without ever handing back a
    /// partner-scope type the visitors desk would reject.</summary>
    private async Task<Guid> AudienceProfileTypeIdAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.Name == name && p.IsForVisitor && p.IsActive);
        if (existing is not null) { return existing.Id; }

        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = name,
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> OrganisationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.Organisations.FirstOrDefaultAsync(o => o.IsActive);
        if (existing is not null) { return existing.Id; }

        var fresh = new SIMF.Domain.Organisations.Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "جهة اختبار",
            Name = "Test Organisation",
            CommercialRegistration = $"CR{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Organisations.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"bf02-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "BF-02 Journey Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
