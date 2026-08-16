// Issue-1 — proves the per-page/per-action permission gate cannot be bypassed
// at the API: a holder of a custom role reaches exactly the endpoints whose
// permission it was granted and is 403'd on the rest, an Administrator reaches
// everything via the wildcard, and a role-less admin reaches nothing.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Api.Endpoints.Admin;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class PermissionEnforcementTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionEnforcementTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Custom_role_reaches_only_its_granted_endpoint()
    {
        // A custom role granted exactly Sessions.View — nothing else.
        var token = await CreateAdminWithCustomRoleAsync(
            grantedCodes: [PermissionCatalog.Sessions.View]);

        // Granted: the sessions list is reachable (not 403).
        var granted = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        // Not granted: the themes list is forbidden, even though both are
        // "admin" endpoints that used to share the AdministratorOnly gate.
        var denied = await PostAuthAsync("/api/v1/admin/themes/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Topping_up_an_order_needs_BulkGenerate_not_ManageBatches()
    {
        // The badge-orders page carries three row actions over TWO different
        // authorities: re-email and revoke are Visitors.ManageBatches, but
        // top-up MINTS badges and so is Visitors.BulkGenerate. Someone trusted
        // to re-send or cancel an order is not thereby trusted to create more of
        // it. The Control Panel hides the button on exactly this split, and the
        // endpoint has to hold the same line or the hidden button is theatre
        // over an open API.
        var token = await CreateAdminWithCustomRoleAsync(
            grantedCodes:
            [
                PermissionCatalog.Visitors.ViewBatches,
                PermissionCatalog.Visitors.ManageBatches,
            ]);

        // Granted: the orders list is reachable, so the role really does work.
        var granted = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        var denied = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = Guid.NewGuid(),
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = Guid.NewGuid(), Count = 1 },
                },
            },
            token);

        // 403 and not 404: the gate is checked before the order is looked up, so
        // a caller without the permission learns nothing about which ids exist.
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Administrator_wildcard_reaches_every_admin_endpoint()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var sessions = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        var themes = await PostAuthAsync("/api/v1/admin/themes/list", new GridQuery(), token);

        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, themes.StatusCode);
    }

    [Fact]
    public async Task Admin_user_with_no_permissions_is_forbidden()
    {
        // A custom role with zero grants → an empty `perm` claim set.
        var token = await CreateAdminWithCustomRoleAsync(grantedCodes: []);

        var denied = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Create_admin_with_roles_requires_the_AssignRoles_permission()
    {
        // A creator granted Admins.Create but deliberately NOT Admins.AssignRoles
        // must not be able to mint an elevated (Administrator) account by passing
        // Roles on the create payload - that would bypass the separate role gate.
        var token = await CreateAdminWithCustomRoleAsync(
            grantedCodes: [PermissionCatalog.Admins.Create]);

        var elevated = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = "priv.escalation@example.com",
                DisplayName = "Priv Escalation",
                Roles = new List<string> { AdministratorRole },
            },
            token);
        Assert.Equal(HttpStatusCode.Forbidden, elevated.StatusCode);

        // The same creator can still create a plain admin with no role grant.
        var plain = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = "plain.newadmin@example.com",
                DisplayName = "Plain Admin",
                Roles = new List<string>(),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
    }

    // Issue-29 — the build-time guard the CLAUDE.md HARD RULE promises but the
    // three behavioural [Fact]s above do not provide: they spot-check two
    // hardcoded routes, so a NEW admin endpoint that forgets its gate ships
    // undetected. This reflection sweep enumerates EVERY mapped route under the
    // /admin/ surface and fails the build if any one is not both permission-gated
    // and approval-gated — "treat a missing permission as a security defect".
    //
    // Scope is by ROUTE (/admin/*), not by the Endpoints/Admin namespace, on
    // purpose: ~90 admin endpoints live outside that folder (Archive, Exhibitors,
    // BusinessMeetings, Attendance, ...), and the app-facing pickers that live
    // *inside* it route under /app/* — so the /admin/ route, not the folder, is
    // the true admin-surface boundary the rule is about.
    //
    // Reads the runtime authorization the middleware actually enforces
    // (IAuthorizeData policy names on the mapped endpoint), so it cannot drift
    // from what Configure() declared.
    [Fact]
    public void Every_admin_endpoint_is_permission_and_approval_gated()
    {
        var adminEndpoints = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Distinct()
            .Where(endpoint => AdminPath(endpoint) is not null)
            .ToList();

        // A reflection sweep that matches nothing passes silently — worse than
        // useless. The admin surface is several hundred mapped routes; guard
        // against a future change (route-prefix rename, data-source move) that
        // would make this test vacuously green.
        Assert.True(adminEndpoints.Count > 100,
            $"Expected the full admin endpoint surface but only matched {adminEndpoints.Count} routes " +
            "— the enumeration is probably broken, not the gates.");

        var ungated = new List<string>();
        foreach (var endpoint in adminEndpoints)
        {
            var path = AdminPath(endpoint)!;
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?
                .HttpMethods.FirstOrDefault() ?? "?";
            var policies = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => !string.IsNullOrEmpty(policy))
                .Select(policy => policy!)
                .ToList();

            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                ungated.Add($"{method} {path} — is AllowAnonymous; an admin endpoint must be authenticated.");
                continue;
            }

            if (!policies.Contains(AuthorizationPolicies.RequireApprovedAccount))
            {
                ungated.Add($"{method} {path} — missing the RequireApprovedAccount gate. " +
                    $"Declared policies: [{string.Join(", ", policies)}].");
            }

            // The /admin/assets/* endpoints declare RequireApprovedAccount and then
            // enforce a *dynamic* per-{category} permission imperatively in the
            // handler (AssetAuth.Has(User, AssetPermissionRegistry.For(category)…)),
            // so a static Policies(PolicyFor(...)) gate is impossible for them.
            // They are still authenticated + approval-gated + permission-checked,
            // just not declaratively — the sole documented carve-out from the
            // permission-policy requirement (see AssetEndpoints.cs).
            var imperativelyGated = path.Contains("/admin/assets/", StringComparison.OrdinalIgnoreCase);
            if (!imperativelyGated && !policies.Any(IsPermissionOrRoleGate))
            {
                ungated.Add($"{method} {path} — missing a permission gate " +
                    "(PermissionCatalog.PolicyFor(...) or a named role policy). " +
                    $"Declared policies: [{string.Join(", ", policies)}]. " +
                    "RequireApprovedAccount alone lets EVERY approved admin in.");
            }
        }

        Assert.True(ungated.Count == 0,
            "These admin endpoints are not fully gated. Add " +
            "Policies(PermissionCatalog.PolicyFor(PermissionCatalog.X.Y), " +
            "nameof(AuthorizationPolicies.RequireApprovedAccount)) to each Configure():" +
            Environment.NewLine + string.Join(Environment.NewLine, ungated));
    }

    /// <summary>
    /// D-834 — an approval route must be gated on the permission for the TIER IT
    /// ACTS ON.
    ///
    /// <para>The test above proves every admin endpoint carries <i>a</i> permission
    /// gate. It cannot see whether that gate is the RIGHT one, which is how this bug
    /// survived: <c>ApproveOtherEndpoint</c> and <c>RejectOtherEndpoint</c> were
    /// copy-pasted from the admin pair above them in the same file and kept the
    /// admin policy line, so approving a PARTNER account demanded
    /// <c>Admins.Approve</c> — the code that exists to approve ADMINS. An admin
    /// granted only the partner queue could not approve a partner one at a time
    /// (only in bulk), and an admin granted only the admin queue could.</para>
    ///
    /// <para>The expectation is DERIVED from the route rather than listed, so a
    /// fourth tier, or a new bulk variant, is covered the day it is mapped. The
    /// three tiers are symmetric by construction: 3 tiers x approve/reject x
    /// single/bulk = the 12 routes asserted here.</para>
    /// </summary>
    [Fact]
    public void An_approval_route_is_gated_on_the_permission_for_the_tier_it_acts_on()
    {
        // Every approval route on the admin surface is classified into exactly one
        // of these two sets, and an unclassified one FAILS. The first version of
        // this test filtered to the tier map before counting, so a new approval
        // route on an unrecognised segment — /admin/accounts/{id}/approve, say, and
        // /admin/accounts/ is already a live segment — was dropped silently while
        // the count stayed at 12 and the test stayed green. Classify, then count.
        var accountTiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["admins"] = "Admins",
            ["others"] = "Others",
            ["visitors"] = "Visitors",
        };

        // Approval routes that do NOT act on an account queue, so the tier rule does
        // not apply. Each entry needs a reason, and a new one has to be argued for
        // here rather than slipping through unnoticed.
        var notAnAccountTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["questions"] =
                "PUT /admin/questions/{id}/approve moderates a QUESTION, not an account "
                + "(Questions.Moderate).",
            ["session-summaries"] =
                "PUT /admin/session-summaries/{id}/approve publishes a session SUMMARY, "
                + "not an account (SessionSummaries.Approve).",
        };

        var approvalRoutes = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Distinct()
            .Select(endpoint => (endpoint, path: AdminPath(endpoint)))
            .Where(candidate => candidate.path is not null
                && ApprovalVerb(candidate.path!) is not null)
            .ToList();

        var unclassified = approvalRoutes
            .Select(route => (route.path, tier: TierSegment(route.path!)))
            .Where(route => !accountTiers.ContainsKey(route.tier)
                && !notAnAccountTier.ContainsKey(route.tier))
            .ToList();

        Assert.True(unclassified.Count == 0,
            "These approval routes are neither an account tier nor a reviewed "
            + "exception, so nothing checks which permission gates them. Add the "
            + "segment to accountTiers (if it is an account queue) or to "
            + "notAnAccountTier with a reason:" + Environment.NewLine
            + string.Join(Environment.NewLine,
                unclassified.Select(route => $"{route.path} — segment '{route.tier}'")));

        var tierRoutes = approvalRoutes
            .Where(route => accountTiers.ContainsKey(TierSegment(route.path!)))
            .ToList();

        // 3 tiers x {approve, reject} x {single, bulk}. A rename that silently
        // stops matching would otherwise leave this test vacuously green.
        Assert.True(tierRoutes.Count == 12,
            $"Expected the 12 account-tier approval routes but matched {tierRoutes.Count}. "
            + "If a tier or a variant was added or renamed, update the count "
            + "deliberately — do not let the sweep match nothing:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, tierRoutes.Select(r => r.path)));

        var wrongTier = new List<string>();
        foreach (var (endpoint, path) in tierRoutes)
        {
            var expected = $"{accountTiers[TierSegment(path!)]}.{ApprovalVerb(path!)}";
            var codes = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => !string.IsNullOrEmpty(policy)
                    && PermissionCatalog.IsPermissionPolicy(policy!))
                .Select(policy => PermissionCatalog.CodeFromPolicy(policy!))
                .ToList();

            if (!codes.Contains(expected, StringComparer.Ordinal))
            {
                wrongTier.Add($"POST {path} acts on the {TierSegment(path!)} tier so it must gate on "
                    + $"{expected}, but gates on [{string.Join(", ", codes)}].");
            }
        }

        Assert.True(wrongTier.Count == 0,
            "These approval routes gate on another tier's permission, so the wrong "
            + "admins can act on the queue:" + Environment.NewLine
            + string.Join(Environment.NewLine, wrongTier));
    }

    /// <summary>"Approve" / "Reject" for an approval route (single or bulk), else null.</summary>
    private static string? ApprovalVerb(string path)
    {
        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        if (last.EndsWith("approve", StringComparison.OrdinalIgnoreCase)) { return "Approve"; }
        if (last.EndsWith("reject", StringComparison.OrdinalIgnoreCase)) { return "Reject"; }
        return null;
    }

    /// <summary>The segment straight after "/admin/" — the tier the route acts on.</summary>
    private static string TierSegment(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var adminIndex = Array.FindIndex(segments,
            segment => segment.Equals("admin", StringComparison.OrdinalIgnoreCase));
        return adminIndex >= 0 && adminIndex + 1 < segments.Length
            ? segments[adminIndex + 1]
            : string.Empty;
    }

    // The normalised "/admin/…" path for an admin-surface endpoint, or null when
    // the route is not under the admin surface (e.g. an /app/* picker that lives
    // in the Endpoints/Admin folder).
    private static string? AdminPath(RouteEndpoint endpoint)
    {
        var raw = endpoint.RoutePattern.RawText;
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var path = "/" + raw.TrimStart('/');
        return path.Contains("/admin/", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    // A policy name that gates by permission (perm:<code>) or by one of the named
    // role policies — either keeps the endpoint out of reach of an admin who
    // holds no matching role. RequireApprovedAccount is deliberately NOT counted:
    // every approved admin satisfies it, so on its own it is not a permission gate.
    private static bool IsPermissionOrRoleGate(string policy) =>
        PermissionCatalog.IsPermissionPolicy(policy)
        || policy is AuthorizationPolicies.AdministratorOnly
                  or AuthorizationPolicies.GatesManage
                  or AuthorizationPolicies.GatesOperate
                  or AuthorizationPolicies.GatesViewOwnReports
                  or AuthorizationPolicies.PublicRelationsAccess;

    // Creates a UserType.Admin user holding a fresh custom role whose only
    // grants are `grantedCodes`, then signs in on the CP audience and returns
    // the access token. The seeder does not run under the Testing host, so the
    // Permission rows for the granted codes are inserted here.
    /// <summary>
    /// D-836 — a tier permission must not reach across into the other tier's
    /// subjects. The national ID / Iqama / passport image is the most sensitive
    /// PII in the system, and the ID-document routes guarded the subject on
    /// <c>UserType</c> alone. D-186 folded Other into <c>UserType.Visitor</c>, so
    /// BOTH <c>/admin/visitors/{id}/id-document</c> and
    /// <c>/admin/others/{id}/id-document</c> passed <c>UserType.Visitor</c> and the
    /// guard separated Admin from everyone else and nothing else.
    ///
    /// <para>The upload path is what makes this provable without a face-gated
    /// image: the family check runs BEFORE the file check, so posting no file at
    /// all returns 400 (missing file) when the subject is in the caller's family
    /// and 404 when it is not. Before the fix both returned 400.</para>
    /// </summary>
    [Theory]
    [InlineData("visitors", PermissionCatalog.Visitors.Edit)]
    [InlineData("others", PermissionCatalog.Others.Edit)]
    public async Task An_id_document_upload_refuses_a_subject_from_the_other_tier(
        string routeTier, string grantedCode)
    {
        var token = await CreateAdminWithCustomRoleAsync(grantedCodes: [grantedCode]);
        var (audienceId, partnerId) = await CreateOneSubjectPerTierAsync();
        var ownTier = routeTier == "visitors" ? audienceId : partnerId;
        var otherTier = routeTier == "visitors" ? partnerId : audienceId;

        // Own tier: the guard passes and the request reaches the file check.
        var own = await PostDummyFileAsync(
            $"/api/v1/admin/{routeTier}/{ownTier}/id-document", token);
        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);

        // Other tier: refused before any file handling, and 404 rather than 403 so
        // the response does not confirm that the subject exists.
        var crossing = await PostDummyFileAsync(
            $"/api/v1/admin/{routeTier}/{otherTier}/id-document", token);
        Assert.Equal(HttpStatusCode.NotFound, crossing.StatusCode);
    }

    [Theory]
    [InlineData("visitors", PermissionCatalog.Visitors.View)]
    [InlineData("others", PermissionCatalog.Others.View)]
    public async Task An_id_document_read_refuses_a_subject_from_the_other_tier(
        string routeTier, string grantedCode)
    {
        // The disclosure half. Neither subject has an image on file, so both
        // answer 404 and this alone cannot tell the guard from "no image" - what
        // it pins is that the cross-tier read is never a 200, and it fails loudly
        // if the route is ever changed to answer 403 (which would confirm the
        // subject exists to an admin outside its tier).
        var token = await CreateAdminWithCustomRoleAsync(grantedCodes: [grantedCode]);
        var (audienceId, partnerId) = await CreateOneSubjectPerTierAsync();
        var otherTier = routeTier == "visitors" ? partnerId : audienceId;

        var crossing = await GetAuthAsync(
            $"/api/v1/admin/{routeTier}/{otherTier}/id-document", token);

        Assert.Equal(HttpStatusCode.NotFound, crossing.StatusCode);
    }

    /// <summary>One audience-tier and one partner-tier subject, each with the
    /// linked ProfileType that <c>IsSubjectInFamilyAsync</c> reads to tell them
    /// apart (partner = a UserProfile whose ProfileType has IsForVisitor=false).</summary>
    private async Task<(Guid AudienceId, Guid PartnerId)> CreateOneSubjectPerTierAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var audienceType = await EnsureProfileTypeAsync(appDb, forVisitor: true);
        var partnerType = await EnsureProfileTypeAsync(appDb, forVisitor: false);

        var audienceId = await CreateSubjectAsync(users, appDb, audienceType);
        var partnerId = await CreateSubjectAsync(users, appDb, partnerType);
        return (audienceId, partnerId);
    }

    private static async Task<Guid> EnsureProfileTypeAsync(SimfAppDbContext appDb, bool forVisitor)
    {
        var existing = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor == forVisitor && p.IsActive);
        if (existing is not null) { return existing.Id; }

        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = forVisitor ? "Visitor — D836Seed" : "Other — D836Seed",
            NameArabic = forVisitor ? "زائر — اختبار" : "أخرى — اختبار",
            PageColor = forVisitor ? "#3B82F6" : "#10B981",
            IsForVisitor = forVisitor,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private static async Task<Guid> CreateSubjectAsync(
        UserManager<SimfUser> users, SimfAppDbContext appDb, Guid profileTypeId)
    {
        var email = $"d836-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            UserType = UserType.Visitor,
            AccountState = AccountState.Approved,
            DisplayName = "D836 Subject",
        };
        // A fixed password, not a GUID-derived one: SIMF's Identity policy rejects
        // three sequential characters, which a random hex string hits often enough
        // to make the seed flaky (it did, on the first run of these tests).
        var created = await users.CreateAsync(user, "Zx9#Qm4$Vk7!");
        Assert.True(created.Succeeded,
            "Could not seed the D-836 subject: "
            + string.Join("; ", created.Errors.Select(e => e.Description)));

        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileTypeId,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A multipart body carrying a deliberately non-image "file" part.
    /// It gets past FastEndpoints' form binding (a body with no parts at all
    /// faults the pipeline) and is then rejected by the MIME/magic-byte gate with
    /// a 400 - which is precisely the "the family guard let me through" signal
    /// these tests need, without a face-gated real photograph.</summary>
    private async Task<HttpResponseMessage> PostDummyFileAsync(string url, string token)
    {
        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("not-an-image"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(bytes, "file", "not-an-image.bin");

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> CreateAdminWithCustomRoleAsync(string[] grantedCodes)
    {
        var email = $"perm-{Guid.NewGuid():N}@simf.test";
        var roleName = $"Limited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in grantedCodes)
            {
                var def = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions
                    .SingleOrDefaultAsync(p => p.Code == code);
                if (permission is null)
                {
                    permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Code = def.Code,
                    };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
            await db.SaveChangesAsync();

            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Limited Admin",
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
        var email = $"perm-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Perm Admin",
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
