// BF-01 (E2E-BF-01-001) — the join nobody owns: the badge the Control Panel
// MINTS and the badge the app's auth ACCEPTS.
//
// Both halves are already tested, separately, and that is the problem.
// DelegatesAndBulkBadgesTests proves bulk-generate writes Approved, QR-bearing
// placeholder accounts. BadgeAuthTests / BadgeSignInTests prove resolve →
// activate → sign-in — but against a UserProfile each of them hand-builds in
// CreateApprovedVisitorAsync. No test has ever carried ONE bulk-generated badge
// across that seam, so the properties the auth leg silently depends on are
// exactly the ones the mint leg could drift on without turning anything red:
// the synthesized "@simf.local" login (the only reason resolve asks the holder
// for an email), AccountState.Approved (the only state the QR resolver accepts),
// the absent password hash (what routes to activation instead of sign-in), and
// the minted QR itself. Each half would stay green while the real journey broke.
//
// So these tests assert hand-offs, not status codes: the QR read off the minted
// badge is the QR resolve accepts, the OTP row belongs to the account that QR
// resolved to, and the session issued at the very end carries the user id
// created by bulk-generate in the very first step.
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

public sealed class Journey01DelegationToSignInTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    /// <summary>The first password the holder chooses at activation. Satisfies the
    /// StrongPassword rule BadgeActivationCompleteRequestValidator applies.</summary>
    private const string HolderPassword = "Zx9#mKp2!";

    /// <summary>What a printed badge actually carries: QrIdMinter's 12-character
    /// Crockford base32 id (no I, L, O, U, 0 or 1). Asserted rather than assumed,
    /// because the app scans this string and posts it back verbatim.</summary>
    private const string MintedQrPattern = "^[23456789ABCDEFGHJKMNPQRSTVWXYZ]{12}$";

    /// <summary>BadgeAuthService.CodeLifetime, as the start step reports it.</summary>
    private const int ActivationCodeLifetimeSeconds = 600;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public Journey01DelegationToSignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>E2E-BF-01-001 — the golden journey, end to end: an administrator
    /// bulk-generates a delegation, a badge is read at the print desk, and its
    /// holder resolves it, activates it with an emailed code, and signs in.
    /// Every step asserts the state the previous one handed it.</summary>
    [Fact]
    public async Task A_bulk_generated_badge_resolves_activates_and_signs_in()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        // A profile type created for this test alone, so "the badges of this type"
        // is exactly this batch and nothing the seeders left behind.
        var (profileTypeId, profileTypeName) = await FreshVisitorProfileTypeAsync();

        // -- 1. Mint the delegation ------------------------------------------
        // Two badges: one is claimed below, the other stays on the desk and proves
        // at the end that activation bound itself to the SCANNED badge alone.
        var generate = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileTypeId, Count = 2 },
                },
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        var generated = (await generate.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(2, generated.Data!.Created);

        var badges = await MintedBadgesAsync(profileTypeId);
        Assert.Equal(2, badges.Count);
        // The per-tier running counter the printed sheet is numbered from.
        Assert.Equal(
            new[] { $"{profileTypeName} #1", $"{profileTypeName} #2" },
            badges.Select(badge => badge.DisplayName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.All(badges, badge =>
        {
            Assert.Matches(MintedQrPattern, badge.QrId);
            // Approved + passwordless + a placeholder login is not cosmetic detail:
            // it is precisely the triple the auth leg branches on further down.
            Assert.Equal(AccountState.Approved, badge.AccountState);
            Assert.False(badge.HasPassword);
            Assert.EndsWith("@simf.local", badge.LoginEmail, StringComparison.OrdinalIgnoreCase);
            Assert.True(badge.IsDelegate);
        });
        // One request, one batch row — the unit a later re-email / revoke acts on.
        Assert.Single(badges.Select(badge => badge.BatchId).Distinct());

        var claimed = badges.Single(badge => badge.DisplayName.EndsWith("#1", StringComparison.Ordinal));
        var unclaimed = badges.Single(badge => badge.DisplayName.EndsWith("#2", StringComparison.Ordinal));

        // -- 2. Read the badge at the print desk -------------------------------
        // The scan station is the first thing outside the minter to consume that QR.
        // If it resolves to a different account than the one bulk-generate wrote,
        // the printed sheet and the database have already diverged.
        var lookup = await GetAuthAsync($"/api/v1/admin/qr-lookup/{claimed.QrId}", adminToken);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var printed = (await lookup.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!;
        Assert.Equal(claimed.QrId, printed.QrId);
        Assert.Equal(claimed.UserId, printed.UserId);
        Assert.Equal(claimed.LoginEmail, printed.Email);
        Assert.Equal(profileTypeName, printed.ProfileTypeName);

        // -- 3. The holder scans it in the app ---------------------------------
        var resolved = await ResolveAsync(claimed.QrId);
        Assert.True(resolved.Found);
        Assert.False(resolved.HasPassword);
        // NeedsEmail is true ONLY because step 1 synthesized an @simf.local login.
        // This assertion is the load-bearing one: it is where the mint leg's choice
        // decides which screen the app shows.
        Assert.True(resolved.NeedsEmail);
        Assert.Null(resolved.MaskedEmail);
        // The greeting names the badge that was minted, not just "an account".
        Assert.Equal(claimed.DisplayName, resolved.DisplayName);

        // -- 4. Activation start: the holder supplies a real email -------------
        var holderEmail = $"journey01-holder-{Guid.NewGuid():N}@example.com";
        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = claimed.QrId, Email = holderEmail });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = (await start.Content
            .ReadFromJsonAsync<ApiResult<BadgeActivationStartResponse>>())!.Data!;
        // The mask echoes the address the holder typed, so the app can show the
        // holder where the code went before they leave the screen.
        Assert.Equal(MaskOf(holderEmail), started.MaskedEmail);
        Assert.Equal(ActivationCodeLifetimeSeconds, started.CodeExpiresInSeconds);

        // The API never returns the code, so read it from SIMF_Identity — and read
        // it BY the user id the QR resolved to. That is the join: the OTP belongs to
        // the bulk-generated account, not merely to "an account that got a code".
        var code = await LatestActivationCodeAsync(claimed.UserId);
        Assert.NotNull(code);

        // -- 5. Activation complete: first password + attach the email ---------
        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = claimed.QrId,
                Code = code!,
                NewPassword = HolderPassword,
                ConfirmPassword = HolderPassword,
            });

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = (await complete.Content
            .ReadFromJsonAsync<ApiResult<BadgeActivationCompleteResponse>>())!.Data!;
        Assert.True(completed.Activated);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var account = await users.FindByIdAsync(claimed.UserId.ToString());
            Assert.NotNull(account);
            // Rebound in place: same account id from step 1, but the placeholder
            // login has become the holder's real, confirmed, password-bearing one.
            Assert.Equal(holderEmail, account!.Email);
            Assert.Equal(holderEmail, account.UserName);
            Assert.True(account.EmailConfirmed);
            Assert.True(await users.HasPasswordAsync(account!));
        }

        // The printed QR must survive activation — the holder keeps using the same
        // physical badge, so a re-mint here would silently invalidate every sheet.
        Assert.Equal(claimed.QrId, await ProfileQrIdAsync(claimed.UserId));

        // -- 6. Sign in with the new credentials -------------------------------
        var badgeSignIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-sign-in",
            new BadgeSignInRequest { QrId = claimed.QrId, Password = HolderPassword });

        Assert.Equal(HttpStatusCode.OK, badgeSignIn.StatusCode);
        var signedIn = (await badgeSignIn.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        // 2FA is off on a freshly minted badge account, so the password step IS the
        // sign-in and tokens come back directly.
        Assert.False(signedIn.MfaRequired);
        Assert.NotNull(signedIn.Tokens);
        Assert.NotEmpty(signedIn.Tokens!.AccessToken);
        // The end of the chain closes on its beginning: the session belongs to the
        // account bulk-generate created, reached only through the badge's QR.
        Assert.Equal(claimed.UserId, signedIn.Tokens.User.Id);
        Assert.Equal(holderEmail, signedIn.Tokens.User.Email);

        // The attached email is a credential in its own right now — the holder can
        // sign in on a new phone without the physical badge.
        var emailSignIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = holderEmail,
                Password = HolderPassword,
                Audience = SignInAudience.App,
            });
        Assert.Equal(HttpStatusCode.OK, emailSignIn.StatusCode);
        var emailSignedIn = (await emailSignIn.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        Assert.NotNull(emailSignedIn.Tokens);
        Assert.Equal(claimed.UserId, emailSignedIn.Tokens!.User.Id);

        // -- The other badge of the same batch is untouched --------------------
        // Activation attached one holder's email and password to ONE badge. Its
        // sibling — same batch, same tier, minted in the same loop — must still be
        // an unclaimed placeholder, or the batch would activate as a block.
        var sibling = await ResolveAsync(unclaimed.QrId);
        Assert.True(sibling.Found);
        Assert.False(sibling.HasPassword);
        Assert.True(sibling.NeedsEmail);
        Assert.Equal(unclaimed.DisplayName, sibling.DisplayName);
    }

    /// <summary>The kill switch has to reach across the same seam: revoking the
    /// batch on the admin side must make the printed badge stop working on the auth
    /// side. Each half is tested alone (revoke disables the accounts; a non-approved
    /// QR is refused), but nothing proves the one causes the other — and a batch of
    /// printed badges that survives its own revocation is a physical-access
    /// incident, not a bug report.</summary>
    [Fact]
    public async Task Revoking_the_batch_stops_the_printed_badge_resolving_activating_or_signing_in()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var (profileTypeId, _) = await FreshVisitorProfileTypeAsync();

        var generate = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileTypeId, Count = 1 },
                },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var badge = Assert.Single(await MintedBadgesAsync(profileTypeId));

        // Live first, so the refusals below cannot pass because the QR was never
        // accepted in the first place.
        Assert.True((await ResolveAsync(badge.QrId)).Found);

        var revoke = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/revoke",
            new AdminRevokeBadgeBatchRequest { BatchId = badge.BatchId },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var revoked = (await revoke.Content
            .ReadFromJsonAsync<ApiResult<AdminRevokeBadgeBatchResponse>>())!.Data!;
        Assert.Equal(1, revoked.RevokedCount);

        // Same QR string, now dead at every auth entry point.
        var afterRevoke = await ResolveAsync(badge.QrId);
        Assert.False(afterRevoke.Found);
        Assert.Null(afterRevoke.DisplayName);

        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest
            {
                QrId = badge.QrId,
                Email = $"journey01-revoked-{Guid.NewGuid():N}@example.com",
            });
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        var startBody = (await start.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthAccountNotFound, startBody.Error!.Code);

        // Sign-in answers with the generic invalid-credentials 401 — a revoked badge
        // must not announce itself as "revoked but real" to whoever picked it up.
        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-sign-in",
            new BadgeSignInRequest { QrId = badge.QrId, Password = HolderPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
        var signInBody = (await signIn.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, signInBody.Error!.Code);
    }

    // -- fixtures --------------------------------------------------------------

    /// <summary>One minted badge, as it exists after bulk-generate: the App-DB
    /// profile fields paired with the Identity-DB account fields. D-157 forbids a
    /// cross-database join, so the two contexts are read separately and matched in
    /// memory — the same shape the production code uses for a cross-store read.</summary>
    private sealed record MintedBadge(
        Guid UserId,
        string QrId,
        Guid BatchId,
        bool IsDelegate,
        string DisplayName,
        string LoginEmail,
        AccountState AccountState,
        bool HasPassword);

    private async Task<IReadOnlyList<MintedBadge>> MintedBadgesAsync(Guid profileTypeId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        var profiles = await appDb.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.ProfileTypeId == profileTypeId
                && profile.QrId != null
                && profile.BadgeBatchId != null)
            .Select(profile => new
            {
                profile.UserId,
                profile.QrId,
                profile.BadgeBatchId,
                profile.IsDelegate,
            })
            .ToListAsync();

        var userIds = profiles.Select(profile => profile.UserId).ToList();
        var accounts = await identityDb.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.AccountState,
                user.PasswordHash,
            })
            .ToListAsync();

        return profiles
            .Join(accounts,
                profile => profile.UserId,
                account => account.Id,
                (profile, account) => new MintedBadge(
                    profile.UserId,
                    profile.QrId!,
                    profile.BadgeBatchId!.Value,
                    profile.IsDelegate,
                    account.DisplayName,
                    account.Email ?? string.Empty,
                    account.AccountState,
                    !string.IsNullOrEmpty(account.PasswordHash)))
            .ToList();
    }

    private async Task<string> ProfileQrIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var qrId = await appDb.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.QrId)
            .SingleAsync();
        return qrId ?? string.Empty;
    }

    private async Task<ResolveBadgeResponse> ResolveAsync(string qrId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resolve-badge", new ResolveBadgeRequest { QrId = qrId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<ResolveBadgeResponse>>())!;
        Assert.True(body.Success);
        return body.Data!;
    }

    /// <summary>The newest unconsumed badge-activation code for this user, recovered
    /// to plaintext. The row stores a keyed hash (M3), so the only way a test can
    /// obtain the six digits the holder would read in their inbox is AuthFlow's
    /// brute-force helper — the same route BadgeAuthTests takes.</summary>
    private async Task<string?> LatestActivationCodeAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var hash = await identityDb.AccountCodes
            .Where(code => code.UserId == userId
                && code.Purpose == AccountCodePurpose.BadgeActivationOtp
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .Select(code => code.Code)
            .FirstOrDefaultAsync();
        return hash is null ? null : AuthFlow.RecoverPlaintextCode(hash);
    }

    /// <summary>BadgeAuthService's display mask: first character + domain.</summary>
    private static string MaskOf(string email) =>
        $"{email[0]}****{email[email.IndexOf('@')..]}";

    /// <summary>An active audience (visitor) profile type created for one test, so
    /// the badges of that type are exactly the batch under test. The name is
    /// truncated the way the existing bulk tests truncate it, and it is returned so
    /// the caller can assert the "{tier} #{n}" badge names against it.</summary>
    private async Task<(Guid Id, string Name)> FreshVisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Journey01 {Guid.NewGuid():N}"[..24],
            NameArabic = "وفد اختبار الرحلة",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(profileType);
        await appDb.SaveChangesAsync();
        return (profileType.Id, profileType.Name);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"journey01-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Journey01 Administrator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
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
