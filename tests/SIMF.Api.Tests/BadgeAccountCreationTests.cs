// An attendee who holds no Identity account creating one from their badge.
//
// This is the ordinary shape after admission moved onto the profile: a badge is
// printed and handed out long before anyone decides they also want the app, so
// "approved attendee, no account" is the normal state and not an error. The
// tests here pin the three things that make it safe rather than merely possible:
//
//   - a badge from a BULK ORDER may be claimed by whoever holds it, because it
//     was handed to a named person under a controlled distribution;
//   - a badge with no order behind it came from the walk-in desk, is in open
//     circulation, and is refused with the SAME shape an unknown badge gets, so
//     the refusal is never an oracle for which badges exist;
//   - the address the code was sent to is pinned server-side, so holding a code
//     cannot bind an address the code was never sent to.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Badges;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class BadgeAccountCreationTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Str0ng!Passw0rd#2026";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeAccountCreationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_bulk_badge_with_no_account_creates_one_and_signs_in()
    {
        var (profileId, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: true);
        var email = $"claimed-{Guid.NewGuid():N}@simf.test";

        // The app sees a real badge that needs an email — the same answer a
        // placeholder walk-in account already produced.
        var resolved = await ResolveAsync(qrId);
        Assert.True(resolved.Found);
        Assert.False(resolved.HasPassword);
        Assert.True(resolved.NeedsEmail);
        Assert.Null(resolved.MaskedEmail);

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, email)).StatusCode);

        var code = await LatestCodeForProfileAsync(profileId);
        Assert.NotNull(code);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(qrId, code!, Password)).StatusCode);

        // The account exists, is linked to the attendee, and signs in.
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await appDb.UserProfiles.AsNoTracking()
                .SingleAsync(p => p.Id == profileId);
            Assert.NotNull(profile.UserId);

            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var created = await users.FindByEmailAsync(email);
            Assert.NotNull(created);
            Assert.Equal(profile.UserId, created!.Id);
            Assert.Equal(AccountState.Approved, created.AccountState);
            Assert.Equal(UserType.Visitor, created.UserType);
            Assert.True(created.EmailConfirmed);
        }

        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password, Audience = SignInAudience.App });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task A_walk_in_badge_with_no_account_cannot_be_claimed_by_whoever_holds_it()
    {
        // No bulk order behind it: the badge is in open circulation, so anyone
        // who photographed it could otherwise claim a full app account from the
        // picture. Refused with the same "badge not recognised" an unknown QR
        // gets, so this is not an oracle for which badges exist.
        var (_, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: false);

        var start = await StartAsync(qrId, $"claim-{Guid.NewGuid():N}@simf.test");
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);

        var unknown = await StartAsync("ZZZZZZZZZZZZ", $"x-{Guid.NewGuid():N}@simf.test");
        Assert.Equal(unknown.StatusCode, start.StatusCode);
    }

    [Fact]
    public async Task Complete_attaches_the_address_the_code_was_sent_to_and_not_one_supplied_later()
    {
        // The whole point of pinning the address at the start step. The complete
        // request carries the code and no address at all, so a caller who
        // intercepts a code cannot redirect the account onto their own inbox.
        var (profileId, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: true);
        var pinned = $"pinned-{Guid.NewGuid():N}@simf.test";

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, pinned)).StatusCode);
        var code = await LatestCodeForProfileAsync(profileId);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(qrId, code!, Password)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var profile = await appDb.UserProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        var account = await users.FindByIdAsync(profile.UserId!.Value.ToString());
        Assert.Equal(pinned, account!.Email);
    }

    [Fact]
    public async Task A_second_start_replaces_the_first_code_and_its_address()
    {
        var (profileId, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: true);
        var first = $"first-{Guid.NewGuid():N}@simf.test";
        var second = $"second-{Guid.NewGuid():N}@simf.test";

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, first)).StatusCode);
        var firstCode = await LatestCodeForProfileAsync(profileId);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, second)).StatusCode);
        var secondCode = await LatestCodeForProfileAsync(profileId);
        Assert.NotEqual(firstCode, secondCode);

        // The superseded code no longer works — a mistyped address is corrected
        // by starting again, and the old one cannot still be redeemed.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await CompleteAsync(qrId, firstCode!, Password)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await CompleteAsync(qrId, secondCode!, Password)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var profile = await appDb.UserProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        var account = await users.FindByIdAsync(profile.UserId!.Value.ToString());
        Assert.Equal(second, account!.Email);
    }

    [Fact]
    public async Task An_account_left_unlinked_by_an_interrupted_attempt_is_adopted_on_retry()
    {
        // The two databases share no transaction: the account is created in
        // Identity and linked in App as two writes. If the link never lands, the
        // holder must not be stuck behind a permanent "email already in use".
        var (profileId, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: true);
        var email = $"orphan-{Guid.NewGuid():N}@simf.test";

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, email)).StatusCode);
        var code = await LatestCodeForProfileAsync(profileId);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(qrId, code!, Password)).StatusCode);

        // Simulate the interrupted attempt by dropping the link the completed
        // run wrote, leaving the account behind with nobody pointing at it.
        Guid orphanId;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await appDb.UserProfiles.SingleAsync(p => p.Id == profileId);
            orphanId = profile.UserId!.Value;
            profile.UserId = null;
            await appDb.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, email)).StatusCode);
        var retryCode = await LatestCodeForProfileAsync(profileId);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(qrId, retryCode!, Password)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await appDb.UserProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
            // Adopted, not duplicated — one account for that address either way.
            Assert.Equal(orphanId, profile.UserId);
        }
    }

    [Fact]
    public async Task Once_the_badge_has_an_account_it_routes_to_the_normal_sign_in()
    {
        var (profileId, qrId) = await CreateAccountlessAttendeeAsync(inBulkOrder: true);
        var email = $"routed-{Guid.NewGuid():N}@simf.test";

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(qrId, email)).StatusCode);
        var code = await LatestCodeForProfileAsync(profileId);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(qrId, code!, Password)).StatusCode);

        // Scanning it again is a returning holder, not a new claim.
        var resolved = await ResolveAsync(qrId);
        Assert.True(resolved.Found);
        Assert.True(resolved.HasPassword);
        Assert.False(resolved.NeedsEmail);
        Assert.NotNull(resolved.MaskedEmail);

        // And a second activation is refused rather than re-creating anything.
        Assert.Equal(HttpStatusCode.Conflict,
            (await StartAsync(qrId, $"other-{Guid.NewGuid():N}@simf.test")).StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<ResolveBadgeResponse> ResolveAsync(string qrId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resolve-badge", new ResolveBadgeRequest { QrId = qrId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<ResolveBadgeResponse>>())!.Data!;
    }

    private Task<HttpResponseMessage> StartAsync(string qrId, string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = qrId, Email = email });

    private Task<HttpResponseMessage> CompleteAsync(string qrId, string code, string password) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = qrId,
                Code = code,
                NewPassword = password,
                ConfirmPassword = password,
            });

    private async Task<string?> LatestCodeForProfileAsync(Guid profileId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var hash = await idDb.AccountCodes
            .Where(c => c.UserProfileId == profileId
                && c.Purpose == AccountCodePurpose.BadgeActivationOtp
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Code)
            .FirstOrDefaultAsync();
        return hash is null ? null : AuthFlow.RecoverPlaintextCode(hash);
    }

    /// <summary>An approved attendee with a printed badge and NO account — what
    /// a bulk order or a walk-in desk actually produces. The bulk order is what
    /// separates a badge that may be self-claimed from one that may not.</summary>
    private async Task<(Guid ProfileId, string QrId)> CreateAccountlessAttendeeAsync(
        bool inBulkOrder)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Everyone belongs to an order; whoever arrived without a bulk one behind
        // them belongs to the seeded direct-registration order, which is exactly
        // what makes them un-claimable from a photographed badge.
        var batchId = BadgeBatch.DirectRegistrationId;
        if (inBulkOrder)
        {
            var batch = new BadgeBatch
            {
                Id = Guid.NewGuid(),
                // No lines: this test only needs an order to belong to, and what
                // the order holds is a child row now rather than a stored string.
                IsDelegate = false,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.BadgeBatches.Add(batch);
            batchId = batch.Id;
        }

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor " + Guid.NewGuid().ToString("N")[..8],
            NameArabic = "زائر",
            PageColor = "#0EA5E9",
            IsForVisitor = true,
            MobileAppRole = MobileAppRole.None,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(profileType);

        var qrId = TestAttendeeProfiles.NewQrId();
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = null,
            ProfileTypeId = profileType.Id,
            Name = "Badge Holder",
            NameArabic = "حامل الشارة",
            NationalityId = 682,
            AdmissionState = AccountState.Approved,
            BadgeBatchId = batchId,
            QrId = qrId,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.UserProfiles.Add(profile);
        await appDb.SaveChangesAsync();
        return (profile.Id, qrId);
    }
}
