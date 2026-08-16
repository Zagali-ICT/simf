// BF-13 — the permission / security matrix, at the API layer.
//
// `PermissionEnforcementTests` already covers four of this flow's twelve
// scenarios: -001 (Administrator wildcard reaches everything), -002's API half
// (a custom role reaches only its grant), -003 (a role-less admin reaches
// nothing) and -011 (the reflection guard that fails the build on an ungated
// admin endpoint). This file adds the six that were unexecuted and that live at
// the API rather than in a browser:
//
//   -004  an app/visitor-audience token is refused on an admin endpoint
//   -005  anonymous is 401; the anonymous AUTH surface is exactly its reviewed
//         allow-list; and no endpoint outside that surface is anonymous at all
//   -006  over-posting a protected field on an UPDATE is ignored (the D-544
//         dual-DTO rule)
//   -007  a baseline role grant behaves as the catalogue declares it
//   -009  a new grant only takes effect on a fresh token mint (JWT-baked)
//   -010  a non-user token is never accepted as a user token
//
// -008 and -012 are browser assertions (deep-link bounce to /not-permitted, and
// that page in Arabic) and belong to the CP sweep, not here.
//
// An ungated admin surface is reachable by ANY signed-in admin regardless of
// role, which the project rules call a security defect outright — so these are
// P0 and they assert denial, which is the direction that actually matters.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class BusinessFlow13PermissionMatrixTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    /// <summary>The reviewed set of endpoints allowed to be reachable without a
    /// bearer token. Anything NOT on this list is a new unauthenticated entry
    /// point and must be a deliberate, reviewed decision — which is what failing
    /// this test forces.
    ///
    /// <para><b>This list is 20 entries, not 3.</b> CLAUDE.md §4 once stated the
    /// rule as "No AllowAnonymous except: SignIn / SignUp / ForgotPassword", and
    /// the first draft of this test encoded that literally and failed against 14
    /// more. None of those is a defect: they are the rest of the
    /// pre-authentication surface, and each carries its own credential instead of
    /// a bearer token — an emailed code, a reset token, a refresh token, a badge
    /// activation code, or a device-key challenge signature. Gating them on a
    /// bearer token would make them unreachable by the only callers that need
    /// them, i.e. it would break sign-up, 2FA and password reset outright. The
    /// rule's spirit is "the authentication surface, and nothing else", and the
    /// project rules were corrected to say so rather than keeping a literal
    /// three-name list that could only be satisfied by breaking authentication.
    /// <b>Keep the count in this sentence and in CLAUDE.md §4 in step with the
    /// list</b> — it has drifted twice (17, then 19) while the array grew.</para>
    ///
    /// <para>The value of the test is unchanged by widening it: a 21st anonymous
    /// auth endpoint still breaks the build. What this list does NOT constrain is
    /// everything outside <c>/auth/</c> — see
    /// <see cref="No_endpoint_outside_the_authentication_surface_is_anonymous"/>,
    /// which is the half that guards <c>/admin/</c>.</para></summary>
    private static readonly string[] ExpectedAnonymousAuthRoutes =
    [
        // The three the rule names.
        "/api/v1/app/auth/sign-in",
        "/api/v1/app/auth/sign-up",
        "/api/v1/app/auth/forgot-password",

        // Email verification — the caller has no token yet by definition; the
        // emailed six-digit code is the credential.
        "/api/v1/app/auth/verify-email",
        "/api/v1/app/auth/resend-code",

        // Second factor — runs BETWEEN credentials and token issue, authorised by
        // the second-factor ticket plus the emailed OTP (visitors) or the
        // authenticator code (admins, D-033).
        "/api/v1/app/auth/verify-otp",
        "/api/v1/app/auth/verify-totp",
        "/api/v1/app/auth/resend-otp",
        "/api/v1/app/auth/verify-recovery-code",

        // #2 (Q1, 2026-07-30) — MANDATORY second-factor enrolment for a Control
        // Panel account that just proved its password and has no authenticator
        // secret. Same position in the flow as the second factor above: it runs
        // between credentials and token issue, so requiring a bearer token here
        // would make it unreachable by the only callers that need it and would
        // lock every unenrolled admin — including the production super-admin —
        // out of the Control Panel permanently. The credential is the single-use,
        // 15-minute, attempt-capped enrolment ticket minted at the password step;
        // it authorises nothing except pairing an authenticator on that one
        // account, and the completion step spends it.
        "/api/v1/app/auth/totp/enrolment/start",
        "/api/v1/app/auth/totp/enrolment/complete",

        // Password reset / forced change — the reset code is the credential.
        "/api/v1/app/auth/reset-password",
        "/api/v1/app/auth/complete-password-change",

        // Refresh — carries a refresh token, not an access token, and its whole
        // purpose is to run once the access token has expired.
        "/api/v1/app/auth/refresh",

        // Badge onboarding (D-737/738) — a printed badge QR is the credential and
        // the holder has no account credentials yet.
        "/api/v1/app/auth/resolve-badge",
        "/api/v1/app/auth/badge-sign-in",
        "/api/v1/app/auth/badge-activation/start",
        "/api/v1/app/auth/badge-activation/complete",

        // Device-key / biometric sign-in — the challenge signature is the
        // credential, and this is how a token is obtained in the first place.
        "/api/v1/app/auth/device-keys/{id:guid}/challenge",
        "/api/v1/app/auth/sign-in-with-device-key",
    ];

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BusinessFlow13PermissionMatrixTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>E2E-BF-13-004 — a visitor signing in on the App audience must not
    /// reach the admin surface. The permission claim set is the gate, and a
    /// visitor has none, so this is 403 (authenticated, not authorised) rather
    /// than 401.</summary>
    [Fact]
    public async Task An_app_audience_visitor_token_is_refused_on_an_admin_endpoint()
    {
        var visitorToken = await CreateApprovedVisitorAndSignInAsync(SignInAudience.App);

        var denied = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), visitorToken);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    /// <summary>E2E-BF-13-005, first half — no token, no admin surface.</summary>
    [Fact]
    public async Task An_anonymous_request_to_an_admin_endpoint_is_401()
    {
        var denied = await _client.PostAsJsonAsync(
            "/api/v1/admin/sessions/list", new GridQuery());

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    /// <summary>E2E-BF-13-005, second half — and the more useful one. The first
    /// asserts one route stays shut; this asserts no NEW door opened. It
    /// enumerates the mapped <c>/auth/</c> surface and requires the anonymous set
    /// to equal <see cref="ExpectedAnonymousAuthRoutes"/> exactly, so a new
    /// AllowAnonymous auth endpoint breaks the build until someone justifies it
    /// by adding it there.
    ///
    /// <para>The name used to say "the three endpoints", which the array it
    /// compares against outgrew long ago — it is 20 routes, each with a written
    /// justification. That mattered enough to fix rather than leave: a failure
    /// message framed as "should be three" invites the reader to delete entries
    /// until it is three, and every one of those deletions breaks sign-up, the
    /// second factor or password reset.</para></summary>
    [Fact]
    public void The_anonymous_auth_surface_is_exactly_its_reviewed_allow_list()
    {
        var anonymous = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Distinct()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(RouteOf)
            .Where(path => path.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A sweep that matches nothing passes silently. The auth surface always
        // has at least the three below, so an empty result means the enumeration
        // broke, not that the gates did.
        Assert.True(
            anonymous.Count > 0,
            "No anonymous auth endpoints matched at all — the enumeration is "
            + "broken, not the gates.");

        var unexpected = anonymous
            .Where(path => !ExpectedAnonymousAuthRoutes.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var missing = ExpectedAnonymousAuthRoutes
            .Where(path => !anonymous.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "New UNAUTHENTICATED auth endpoint(s) — every one of these is reachable "
            + "with no token by anyone on the network. Justify and add to "
            + $"ExpectedAnonymousAuthRoutes, or gate it:\n  {string.Join("\n  ", unexpected)}");
        Assert.True(
            missing.Count == 0,
            "Expected-anonymous auth endpoint(s) are no longer anonymous — sign-in "
            + "or sign-up may now be unreachable for new users:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>E2E-BF-13-005, third part — the half the allow-list above cannot
    /// reach. That test filters to <c>/auth/</c> before it compares, which is
    /// correct for what it pins but leaves every other route unexamined: an
    /// <c>AllowAnonymous</c> on an admin endpoint passes it without complaint.
    /// This asserts the two things that are never legitimate anywhere.
    ///
    /// <para><b>Admin routes.</b> The whole <c>/admin/</c> surface is
    /// permission-gated by design, and the project rules call an ungated admin
    /// endpoint a security defect outright. There is no reviewed exception, so
    /// this is an absolute rule and not a second allow-list.</para>
    ///
    /// <para><b>Anonymous AND policy-gated is a contradiction that resolves the
    /// dangerous way.</b> ASP.NET Core lets <c>AllowAnonymous</c> short-circuit
    /// authorization, so an endpoint carrying both reads as gated to anyone
    /// reviewing it — the policy is right there in the source — while being open
    /// in fact. That is strictly worse than a plainly ungated endpoint, because
    /// the gate is what a reviewer checks for. It cannot be caught by reading the
    /// endpoint, only by asking the built routing table, which is what this
    /// does.</para>
    ///
    /// <para>Deliberately NOT asserted here: the full public-read surface
    /// (sessions, news, media and the rest). Those are legitimately anonymous and
    /// change with ordinary content work, so pinning them by name would be a
    /// list nobody maintains. The rule this encodes is the invariant one.</para></summary>
    [Fact]
    public void No_endpoint_outside_the_authentication_surface_is_anonymous()
    {
        var anonymous = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Distinct()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .ToList();

        // A sweep that matches nothing passes silently, so prove the enumeration
        // itself still works before trusting either verdict below.
        Assert.True(
            anonymous.Count > 0,
            "No anonymous endpoints matched at all — the enumeration is broken, "
            + "not the gates.");

        var adminRoutes = anonymous
            .Select(RouteOf)
            .Where(path => path.Contains("/admin/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            adminRoutes.Count == 0,
            "ADMIN endpoint(s) are reachable with no token by anyone on the "
            + "network. An admin route is permission-gated by design and there is "
            + "no reviewed exception — gate it, do not add an allow-list:\n  "
            + string.Join("\n  ", adminRoutes));

        // Read the DECLARED intent, not the built authorization metadata. When an
        // endpoint declares AllowAnonymous, FastEndpoints emits no IAuthorizeData
        // for it at all — the policy is dropped during route building, so the
        // contradiction is invisible in the routing table and the obvious version
        // of this check silently passes. Verified by probing a planted endpoint:
        // IAllowAnonymous=true, IAuthorizeData count=0, while the definition still
        // carried PreBuiltUserPolicies=[RequireApprovedAccount]. The definition is
        // therefore the only place the two declarations coexist.
        var policyGated = anonymous
            .Select(endpoint => (
                Route: RouteOf(endpoint),
                Definition: endpoint.Metadata.GetMetadata<FastEndpoints.EndpointDefinition>()))
            .Where(entry => entry.Definition?.PreBuiltUserPolicies is { Count: > 0 })
            .Select(entry => entry.Route)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            policyGated.Count == 0,
            "Endpoint(s) declare an authorization policy AND AllowAnonymous. "
            + "AllowAnonymous wins and the policy is discarded, so these are OPEN "
            + "while reading as gated to anyone reviewing the source — remove "
            + "whichever of the two is wrong:\n  "
            + string.Join("\n  ", policyGated));
    }

    private static string RouteOf(RouteEndpoint endpoint) =>
        "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

    /// <summary>E2E-BF-13-006 — the D-544 dual-DTO rule. An admin UPDATE binds its
    /// OWN request DTO and maps explicitly, so a field the caller is not supposed
    /// to set cannot be smuggled in by adding it to the JSON body. Asserted on the
    /// speaker update, the endpoint that rule was written for.</summary>
    [Fact]
    public async Task Over_posting_an_unmapped_field_on_a_speaker_update_is_ignored()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var code = $"BF13-{Guid.NewGuid().ToString("N")[..8]}";
        var created = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = code,
                Name = "Over-post Probe",
                NameArabic = "مسبار",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var speaker = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        // A raw body carrying the legitimate fields PLUS ones the update contract
        // does not expose. If the endpoint bound the entity (or a shared DTO), one
        // of these would stick.
        //
        // WHAT COUNTS AS OVER-POSTING HERE, established the hard way. Three of the
        // fields this test first tried to smuggle — `code`, `isActive` and
        // `userProfileId` — are all REAL properties of AdminUpdateSpeakerRequest.
        // Renaming a code, deactivating a speaker and re-linking an account are
        // supported admin actions, so their taking effect is correct behaviour and
        // the assertions against them were wrong, not the endpoint. The genuine
        // over-post surface is only what the update contract does NOT expose:
        // the route-bound `id` and the server-owned `createdAt`.
        var smuggled = new Dictionary<string, object?>
        {
            // Legitimate fields, sent at their real values so the update succeeds.
            ["code"] = code,
            ["name"] = "Over-post Probe (renamed)",
            ["nameArabic"] = "مسبار",
            ["isActive"] = true,
            // The smuggled pair — neither is on AdminUpdateSpeakerRequest.
            ["id"] = Guid.NewGuid(),
            ["createdAt"] = SimfClock.Now.AddYears(-5),
        };
        var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/admin/speakers/{speaker.Id}")
        {
            Content = JsonContent.Create(smuggled),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updated = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var reread = await GetAuthAsync($"/api/v1/admin/speakers/{speaker.Id}", token);
        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
        var after = (await reread.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        // The route id wins over the body id, the server-owned CreatedAt is
        // untouched, and the field that IS on the contract did apply — proving the
        // update read its own DTO rather than binding whatever arrived.
        Assert.Equal(speaker.Id, after.Id);
        Assert.Equal(speaker.CreatedAt, after.CreatedAt);
        Assert.Equal("Over-post Probe (renamed)", after.Name);
    }

    /// <summary>Not a BF-13 scenario — a defect BF-13's over-posting probe walked
    /// into. Sending a <c>userProfileId</c> that does not exist returned <b>500</b>.
    /// <c>Speaker.UserProfileId</c> is a real same-database FK with
    /// <c>OnDelete.Restrict</c>, so the unknown id blew up at SaveChanges; the
    /// service's own summary claimed the link was cross-context and that a stale
    /// id "degrades gracefully", which stopped being true when <c>UserProfile</c>
    /// moved onto the App context. An admin typo should be a 400 they can act on,
    /// not a 500.</summary>
    [Fact]
    public async Task A_speaker_update_with_an_unknown_user_profile_is_400_not_500()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = $"BF13U-{Guid.NewGuid().ToString("N")[..8]}";
        var created = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = code,
                Name = "FK Probe",
                NameArabic = "مسبار",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var speaker = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerDetail>>())!.Data!;

        var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/admin/speakers/{speaker.Id}")
        {
            Content = JsonContent.Create(new AdminUpdateSpeakerRequest
            {
                Code = code,
                Name = "FK Probe",
                NameArabic = "مسبار",
                UserProfileId = Guid.NewGuid(), // nothing behind it
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Create is the same code path and had the same hole.</summary>
    [Fact]
    public async Task A_speaker_create_with_an_unknown_user_profile_is_400_not_500()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var created = await PostAuthAsync(
            "/api/v1/admin/speakers",
            new AdminCreateSpeakerRequest
            {
                Code = $"BF13C-{Guid.NewGuid().ToString("N")[..8]}",
                Name = "FK Probe Create",
                NameArabic = "مسبار",
                UserProfileId = Guid.NewGuid(),
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    /// <summary>E2E-BF-13-007 — a baseline role holds what the catalogue says it
    /// holds, and nothing more. Public Relations is the example the flow names:
    /// it owns News but has no business reading the session programme.</summary>
    [Fact]
    public async Task A_Public_Relations_admin_reaches_News_but_not_Sessions()
    {
        var token = await CreateAdminWithCustomRoleAsync(
            [PermissionCatalog.News.View]);

        var news = await PostAuthAsync("/api/v1/admin/news/list", new GridQuery(), token);
        var sessions = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);

        Assert.Equal(HttpStatusCode.OK, news.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sessions.StatusCode);
    }

    /// <summary>E2E-BF-13-009 — permissions are baked into the JWT at mint time,
    /// so granting one does NOT widen a token already in the wild. Both halves
    /// matter: the old token must stay denied (no silent live escalation), and a
    /// fresh sign-in must pick the grant up (the grant is not inert).</summary>
    [Fact]
    public async Task A_new_grant_takes_effect_only_on_a_freshly_minted_token()
    {
        var (email, roleName) = await CreateAdminWithEmptyRoleAsync();
        var staleToken = await SignInCpAsync(email);

        var deniedBefore = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), staleToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedBefore.StatusCode);

        await GrantAsync(roleName, PermissionCatalog.Sessions.View);

        // The token minted BEFORE the grant carries the old claim set.
        var deniedAfter = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), staleToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAfter.StatusCode);

        // A fresh mint carries the new one.
        var freshToken = await SignInCpAsync(email);
        var allowed = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), freshToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    /// <summary>E2E-BF-13-010 — a structurally valid but non-user bearer value is
    /// refused, not merely unauthorised. Covers the garbage-token path generally:
    /// anything that is not a token this API minted gets 401.</summary>
    [Theory]
    [InlineData("not-a-token")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9.")] // alg=none forgery
    public async Task A_token_this_API_did_not_mint_is_401(string bearer)
    {
        var denied = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery(), bearer);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private async Task<(string Email, string RoleName)> CreateAdminWithEmptyRoleAsync()
    {
        var email = $"bf13-{Guid.NewGuid():N}@simf.test";
        var roleName = $"BF13-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        await roleManager.CreateAsync(new SimfRole { Name = roleName, IsBaseline = false });

        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "BF13 Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        await users.AddToRoleAsync(user, roleName);
        return (email, roleName);
    }

    private async Task GrantAsync(string roleName, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var role = await db.Roles.SingleAsync(r => r.Name == roleName);

        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == code);
        if (permission is null)
        {
            var def = PermissionCatalog.All.Single(p => p.Code == code);
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
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdminWithCustomRoleAsync(string[] grantedCodes)
    {
        var (email, roleName) = await CreateAdminWithEmptyRoleAsync();
        foreach (var code in grantedCodes)
        {
            await GrantAsync(roleName, code);
        }
        return await SignInCpAsync(email);
    }

    private async Task<string> CreateApprovedVisitorAndSignInAsync(SignInAudience audience)
    {
        var email = $"bf13-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "BF13 Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = audience,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(
            body.Data?.Tokens?.AccessToken is not null,
            "The visitor fixture did not receive a token; the audience or "
            + "account-state rules may have changed.");
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"bf13-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "BF13 Administrator",
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
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
