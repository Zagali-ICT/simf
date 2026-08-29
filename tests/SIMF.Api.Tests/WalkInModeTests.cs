// Tests: D-819 walk-in mode — the standby capability for an event where a large
// crowd arrives who never registered online.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-819 — the walk-in mode ships DISARMED. These tests pin the default: with
/// no configuration the desk behaves exactly as it did before, so nothing about
/// the approval workflow changes until the capability is deliberately armed in
/// appsettings / set-env-*.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Badges)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class WalkInModeTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public WalkInModeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Walk_in_registration_still_lands_pending_with_no_qr_when_the_mode_is_disarmed()
    {
        // The whole point of a standby capability: OFF must be byte-identical to
        // the behaviour D-425 established (every visitor passes an approval
        // review, and the QR is minted on approval, not at the desk).
        var token = await CreateAdminTokenAsync();
        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());

        using var message = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/visitors/register-onsite")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!;

        // No QR at the desk — it is minted on approval.
        Assert.Equal(string.Empty, body.QrId);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var created = await identity.Users.AsNoTracking()
            .SingleAsync(u => u.Id == body.UserId);
        Assert.Equal(AccountState.PendingApproval, created.AccountState);
    }

    [Fact]
    public async Task Walk_in_badge_activation_is_refused_while_the_account_has_no_real_email()
    {
        // D-819 security item. The activation start step lets a QR holder
        // nominate the address the code is sent to when the account carries a
        // synthesized placeholder email. With walk-in badges in circulation that
        // is an account-takeover primitive: photograph a badge, claim the
        // account. Refused with the same "badge not recognised" an unknown QR
        // returns, so it is not an oracle for which badges exist.
        var token = await CreateAdminTokenAsync();
        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync(),
            withEmail: false);

        using var registerMessage = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/visitors/register-onsite")
        {
            Content = JsonContent.Create(request),
        };
        registerMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var registered = await _client.SendAsync(registerMessage);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var walkIn = (await registered.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!;

        // Approve so the account has a QR to attempt activation against.
        var qrId = await ApproveAndReadQrAsync(token, walkIn.UserId);
        Assert.False(string.IsNullOrWhiteSpace(qrId));

        var activation = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest
            {
                QrId = qrId!,
                Email = "attacker@example.test",
            });

        Assert.Equal(HttpStatusCode.NotFound, activation.StatusCode);
    }

    [Fact]
    public async Task Quick_register_is_refused_while_the_mode_is_disarmed()
    {
        // The reduced field set must not be reachable by simply omitting fields:
        // with the mode off the desk's original presence rules still apply.
        var token = await CreateAdminTokenAsync();
        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());
        request.ArabicName = string.Empty;
        request.NationalityCode = string.Empty;
        request.OrganisationId = null;
        request.SaudiMobile = null;

        var response = await PostWalkInAsync(_client, token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Quick_register_accepts_a_name_and_one_id_when_the_mode_is_armed()
    {
        // Armed: name + profile type + one identity document is the floor. The
        // organisation, nationality and mobile are all dropped.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());
        request.ArabicName = string.Empty;
        request.NationalityCode = string.Empty;
        request.OrganisationId = null;
        request.SaudiMobile = null;

        var response = await PostWalkInAsync(client, token, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.AsNoTracking()
            .SingleAsync(p => p.UserId == body.UserId);

        // Both name columns are NOT NULL, so the single captured name is mirrored.
        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        Assert.False(string.IsNullOrWhiteSpace(profile.NameArabic));
        // 0 is the documented "no nationality chosen" value for a stub row.
        Assert.Equal(0, profile.NationalityId);
        Assert.Null(profile.OrganisationId);
    }

    [Fact]
    public async Task Quick_register_still_demands_an_identity_document()
    {
        // The one field quick mode keeps: it is the only thing preventing one
        // person from collecting several badges, and the encrypted columns cannot
        // be reconstructed after the event.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());
        request.NationalId = null;
        request.IqamaNumber = null;
        request.PassportNumber = null;

        var response = await PostWalkInAsync(client, token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Quick_register_still_rejects_a_malformed_identity_document()
    {
        // Shape rules stayed in the validator and always apply: a mistyped id
        // would create a false-unique row and defeat duplicate detection forever.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());
        request.NationalId = "1234567890"; // right shape, wrong Luhn check digit

        var response = await PostWalkInAsync(client, token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Auto_approve_mints_the_qr_at_the_desk_when_the_mode_is_armed()
    {
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var request = BuildWalkInRequest(
            await ResolveVisitorProfileTypeIdAsync(), await ResolveOrganisationIdAsync());

        var response = await PostWalkInAsync(client, token, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!;

        // The badge is usable immediately — that is the whole point of the switch.
        Assert.False(string.IsNullOrWhiteSpace(body.QrId));

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var created = await identity.Users.AsNoTracking()
            .SingleAsync(u => u.Id == body.UserId);
        Assert.Equal(AccountState.Approved, created.AccountState);
    }

    /// <summary>Arms the walk-in mode through configuration, which is the only
    /// way it can be armed — there is deliberately no write endpoint.</summary>
    private WebApplicationFactory<Program> CreateArmedFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WalkInMode:Enabled", "true");
            builder.UseSetting("WalkInMode:QuickRegister", "true");
            builder.UseSetting("WalkInMode:AutoApprove", "true");
            builder.UseSetting("WalkInMode:SessionWalkIn", "true");
        });

    private static Task<HttpResponseMessage> PostWalkInAsync(
        HttpClient client, string token, AdminWalkInRegistrationRequest request)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/visitors/register-onsite")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(message);
    }

    private async Task<string?> ApproveAndReadQrAsync(string token, Guid userId)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/visitors/{userId}/approve")
        {
            Content = JsonContent.Create(new { }),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.QrId)
            .SingleAsync();
    }

    private async Task<Guid> ResolveVisitorProfileTypeIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.ProfileTypes.AsNoTracking()
            .Where(p => p.IsActive && p.IsForVisitor)
            .Select(p => p.Id)
            .FirstAsync();
    }

    /// <summary>The desk validator requires an organisation (D-221), so the
    /// fixture needs one to exist. Creates it on first use.</summary>
    private async Task<Guid> ResolveOrganisationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await db.Organisations.AsNoTracking()
            .Where(o => o.IsActive)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync();
        if (existing is { } found) { return found; }

        var organisation = new SIMF.Domain.Organisations.Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Walk-In Test Org",
            NameArabic = "جهة اختبار",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync();
        return organisation.Id;
    }

    private static AdminWalkInRegistrationRequest BuildWalkInRequest(
        Guid profileTypeId, Guid organisationId, bool withEmail = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new AdminWalkInRegistrationRequest
        {
            Email = withEmail ? $"walkin-{suffix}@simf.test" : null,
            DisplayName = "Walk In Visitor",
            ArabicName = "زائر مباشر",
            EnglishName = "Walk In Visitor",
            ProfileTypeId = profileTypeId,
            OrganisationId = organisationId,
            NationalityCode = "SA",
            IsSaudi = true,
            NationalId = BuildLuhnNationalId(),
            SaudiMobile = "0512345678",
            InterestIds = [],
        };
    }

    /// <summary>A syntactically valid Saudi national id (10 digits, leading 1,
    /// Luhn-correct) — the desk validator keeps the checksum even in quick mode,
    /// because since D-945 it is the only thing standing between a mistyped id and
    /// the badge printed from it.</summary>
    private static string BuildLuhnNationalId()
    {
        var random = new Random(Guid.NewGuid().GetHashCode());
        var digits = new int[10];
        digits[0] = 1;
        for (var i = 1; i < 9; i++) { digits[i] = random.Next(0, 10); }

        for (var check = 0; check < 10; check++)
        {
            digits[9] = check;
            var sum = 0;
            var doubleDigit = false;
            for (var i = 9; i >= 0; i--)
            {
                var value = digits[i];
                if (doubleDigit)
                {
                    value *= 2;
                    if (value > 9) { value -= 9; }
                }
                sum += value;
                doubleDigit = !doubleDigit;
            }
            if (sum % 10 == 0) { break; }
        }

        return string.Concat(digits);
    }

    private Task<string> CreateAdminTokenAsync() => CreateAdminTokenAsync(_client);

    private async Task<string> CreateAdminTokenAsync(HttpClient client)
    {
        const string administratorRole = "Administrator";
        var email = $"walkin-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider
                .GetRequiredService<RoleManager<SimfRole>>();
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
                DisplayName = "Walk-In Desk Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, administratorRole);
        }

        // The Control Panel password step now answers with a TOTP challenge, not
        // tokens — this fixture used to read `.Tokens` straight off the sign-in
        // response, which stopped resolving the moment the enrolment gate landed.
        // AuthFlow enrols the account and answers the challenge with a genuine
        // code, so these tests exercise the shipping two-factor path like every
        // other admin fixture in this assembly.
        return await AuthFlow.SignInControlPanelAsync(
            client, _factory, email, AuthFlow.Password);
    }
}
