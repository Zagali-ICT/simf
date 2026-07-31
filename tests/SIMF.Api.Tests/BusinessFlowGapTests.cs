// Gaps found by the 2026-07-29 business-flow coverage map
// (docs/tests/SIMF-BF-Coverage-Map-2026-07-29.md), closed here.
//
// Each of these is a scenario the flow catalogue has described for months and
// that NOTHING asserted. They are gathered in one file because they share no
// feature, only a cause: every one of them is a guard that the existing tests
// walk past rather than into.
//
// The two worth reading the reasoning for:
//
//   BF-01-006 — bulk badge generation refuses an elevated ProfileType. The guard
//   is real and carries its own message, but the only test near it passes a
//   RANDOM Guid, which trips the "profile type not found" branch above it. So the
//   least-privilege check itself had never once executed.
//
//   BF-03-004 — a gate scan by a Staff operator WITHOUT Gates.Operate. Every
//   existing scan test signs in an Administrator, whose wildcard "*" satisfies
//   the policy, so the gate was only ever tested from the allowed side. This is
//   the same blind spot the Control Panel had until the restricted-role fixture
//   landed: a suite that only drives privileged accounts proves the ALLOW half
//   of an access-control system and quietly assumes the other.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class BusinessFlowGapTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BusinessFlowGapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // ---------------------------------------------------------------- BF-01

    /// <summary>E2E-BF-01-005 — the 1000-badge cap per request, and zero rows
    /// written when it is exceeded. An unbounded count is a resource-exhaustion
    /// hole: the handler mints a real account per badge.</summary>
    [Fact]
    public async Task Bulk_generate_beyond_the_1000_cap_is_rejected_and_writes_nothing()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await FirstVisitorProfileTypeIdAsync();
        var before = await CountUsersAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches = [new BulkBadgeBatch { ProfileTypeId = profileTypeId, Count = 1001 }],
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountUsersAsync());
    }

    /// <summary>E2E-BF-01-005, the boundary. 1000 is allowed, 1001 is not — assert
    /// the edge is where it is documented, without minting 1000 accounts: two
    /// batches summing past the cap prove the check sums ACROSS batches rather
    /// than per batch, which is the part a single-batch test would miss.</summary>
    [Fact]
    public async Task The_bulk_cap_is_summed_across_batches_not_applied_per_batch()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await FirstVisitorProfileTypeIdAsync();
        var before = await CountUsersAsync();

        // Each batch is individually under the cap; together they exceed it.
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches =
                [
                    new BulkBadgeBatch { ProfileTypeId = profileTypeId, Count = 600 },
                    new BulkBadgeBatch { ProfileTypeId = profileTypeId, Count = 600 },
                ],
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountUsersAsync());
    }

    /// <summary>E2E-BF-01-006 — the least-privilege guard, executed for the first
    /// time. A bulk Approved badge of an elevated MobileAppRole would hand out
    /// QR-accessible elevated authority, so the endpoint refuses any ProfileType
    /// that is not an audience (visitor) type.
    ///
    /// <para>The distinction from the existing near-miss test matters: that one
    /// passes <c>Guid.NewGuid()</c>, which is caught by the "profile type not
    /// found" branch ABOVE this check. Both return the same error code, so it
    /// looked covered. This test uses a REAL, ACTIVE, non-visitor type, which is
    /// the only way to reach the guard.</para></summary>
    [Fact]
    public async Task Bulk_generate_refuses_a_real_but_elevated_profile_type()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var partnerTypeId = await FirstNonVisitorProfileTypeIdAsync();
        Assert.NotEqual(Guid.Empty, partnerTypeId);
        var before = await CountUsersAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches = [new BulkBadgeBatch { ProfileTypeId = partnerTypeId, Count = 1 }],
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body!.Error!.Code);
        Assert.Equal(before, await CountUsersAsync());
    }

    // ---------------------------------------------------------------- BF-03

    /// <summary>E2E-BF-03-004 — a signed-in operator WITHOUT <c>Gates.Operate</c>
    /// cannot record a scan.
    ///
    /// <para>Every other scan test signs in an Administrator, whose wildcard
    /// satisfies the policy, so this endpoint's gate had only ever been exercised
    /// from the allowed side. An ungated scan endpoint would let any signed-in
    /// account admit people to the venue.</para></summary>
    [Fact]
    public async Task A_scan_by_an_account_without_Gates_Operate_is_forbidden()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var gate = await CreateGateAsync(adminToken);
        var qr = await CreateApprovedVisitorWithQrAsync();

        // Granted something real, but NOT Gates.Operate.
        var weakToken = await CreateAdminWithGrantAsync(PermissionCatalog.Sessions.View);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gate.Id}/scans")
        {
            Content = JsonContent.Create(new GateScanRequest
            {
                Qr = qr,
                Source = ScanSource.Simulator,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", weakToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And nothing was recorded — a denied caller must not leave a scan row.
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(
            await appDb.GateScans.AnyAsync(scan => scan.GateId == gate.Id),
            "A forbidden scan still wrote a GateScan row.");
    }

    // ---------------------------------------------------------------- BF-04

    /// <summary>E2E-BF-04-002 — a hall code is normalised to upper case on save,
    /// so lookups and the duplicate check below cannot be defeated by casing.</summary>
    [Fact]
    public async Task A_hall_code_is_uppercased_on_save()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var lower = $"bfh{Guid.NewGuid().ToString("N")[..6]}".ToLowerInvariant();

        var created = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = lower,
                Name = "Gap Hall",
                NameArabic = "قاعة",
                Capacity = 50,
            },
            token);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var hall = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        Assert.Equal(lower.ToUpperInvariant(), hall.Code);
    }

    /// <summary>E2E-BF-04-003 — a duplicate hall code is refused, and refused
    /// case-insensitively (which is what makes the uppercase rule load-bearing
    /// rather than cosmetic).</summary>
    [Fact]
    public async Task A_duplicate_hall_code_is_refused_regardless_of_casing()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = $"BFH{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();

        var first = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = code, Name = "Gap Hall", NameArabic = "قاعة", Capacity = 50,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = code.ToLowerInvariant(),   // same code, different casing
                Name = "Gap Hall Twin", NameArabic = "قاعة", Capacity = 50,
            },
            token);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    // ---------------------------------------------------------------- BF-09

    /// <summary>E2E-BF-09-003 — snapshotting with <c>MakeVisible=false</c> creates
    /// the edition WITHOUT publishing it. The inverse is already tested; this side
    /// is the one that matters, because getting it wrong publishes an unfinished
    /// archive to the public site the moment an admin takes a snapshot.</summary>
    [Fact]
    public async Task A_snapshot_with_MakeVisible_false_does_not_publish_the_archive()
    {
        var token = await CreateAdministratorAndSignInAsync();

        // Start from a known-hidden state so the assertion cannot pass by accident
        // on an archive that was already hidden for some other reason.
        var hide = await PutAuthAsync(
            "/api/v1/admin/archive/visibility",
            new UpdateArchiveVisibilityRequest { IsVisible = false },
            token);
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);

        try
        {
            var snapshot = await PostAuthAsync(
                "/api/v1/admin/archive/snapshot-current",
                new SnapshotCurrentEditionRequest { MakeVisible = false },
                token);

            // 200 (created) or 409 (an edition for this year already exists) both
            // leave the visibility question intact, which is what is under test.
            Assert.True(
                snapshot.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                $"snapshot-current returned {snapshot.StatusCode}.");

            var publicList = await _client.GetAsync("/api/v1/app/archive");
            Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
            var archive = (await publicList.Content
                .ReadFromJsonAsync<ApiResult<PublicArchive>>())!.Data!;
            Assert.Empty(archive.Items);
        }
        finally
        {
            // The visibility toggle is GLOBAL state, so leaving it off would make
            // every later archive test see an empty public list and pass or fail
            // for the wrong reason. ArchiveTests restores it the same way.
            await PutAuthAsync(
                "/api/v1/admin/archive/visibility",
                new UpdateArchiveVisibilityRequest { IsVisible = true },
                token);
        }
    }

    // ---------------------------------------------------------------- fixtures

    private async Task<Guid> FirstVisitorProfileTypeIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var id = await appDb.ProfileTypes
            .Where(type => type.IsActive && type.IsForVisitor)
            .OrderBy(type => type.Name)
            .Select(type => type.Id)
            .FirstOrDefaultAsync();
        Assert.NotEqual(Guid.Empty, id);
        return id;
    }

    /// <summary>A REAL, active, non-visitor profile type — the only input that
    /// reaches the least-privilege guard rather than the not-found branch.</summary>
    private async Task<Guid> FirstNonVisitorProfileTypeIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.ProfileTypes
            .Where(type => type.IsActive && !type.IsForVisitor)
            .OrderBy(type => type.Name)
            .Select(type => type.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<int> CountUsersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await db.Users.CountAsync();
    }

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"BFG-{Guid.NewGuid().ToString("N")[..8]}",
                Name = "Gap Test Gate",
                NameArabic = "بوابة",
                DirectionMode = DirectionMode.Both,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<string> CreateApprovedVisitorWithQrAsync()
    {
        var email = $"bfgap-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Gap Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            Name = "Gap Visitor",
            NameArabic = "زائر",
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    /// <summary>An approved admin holding exactly one permission — deliberately
    /// not the one under test.</summary>
    private async Task<string> CreateAdminWithGrantAsync(string code)
    {
        var email = $"bfgap-{Guid.NewGuid():N}@simf.test";
        var roleName = $"BFGap-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == code);
            if (permission is null)
            {
                var def = PermissionCatalog.All.Single(p => p.Code == code);
                permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    Page = def.Page,
                    Action = def.Action,
                    DisplayName = def.DisplayName,
                };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();
            }
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
            });
            await db.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Gap Weak Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }
        return await SignInCpAsync(email);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"bfgap-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(
                    new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Gap Administrator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await SignInCpAsync(email);
    }

    private async Task<string> SignInCpAsync(string email)
    {
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
