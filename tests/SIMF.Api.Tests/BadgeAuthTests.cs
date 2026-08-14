using System.Net;
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

/// <summary>
/// Part B integration tests for badge-QR sign-in / activation
/// (<c>/app/auth/resolve-badge</c>, <c>.../badge-activation/{start,complete}</c>).
/// Covers the resolve branch (found / has-password / needs-email), the
/// placeholder-email activation chain (enter email → code → set password →
/// sign in), and the already-activated + bad-code guards.
/// </summary>
public sealed class BadgeAuthTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeAuthTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Resolve_unknown_badge_returns_not_found()
    {
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resolve-badge", new ResolveBadgeRequest { QrId = "ZZZZZZZZZZZZ" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<ResolveBadgeResponse>>())!;
        Assert.True(body.Success);
        Assert.False(body.Data!.Found);
    }

    [Fact]
    public async Task Resolve_passwordless_walkin_needs_email()
    {
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: null);

        var body = await ResolveAsync(qrId);
        Assert.True(body.Found);
        Assert.False(body.HasPassword);
        Assert.True(body.NeedsEmail);
        Assert.Null(body.MaskedEmail);
    }

    [Fact]
    public async Task Resolve_passwordless_with_real_email_does_not_need_email()
    {
        var email = $"vip-{Guid.NewGuid():N}@example.com";
        var (_, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: email);

        var body = await ResolveAsync(qrId);
        Assert.True(body.Found);
        Assert.False(body.HasPassword);
        Assert.False(body.NeedsEmail);
        Assert.NotNull(body.MaskedEmail);
        Assert.Contains("****", body.MaskedEmail!);
    }

    [Fact]
    public async Task Resolve_account_with_password_routes_to_normal_sign_in()
    {
        var (_, qrId) = await CreateApprovedVisitorAsync(
            withPassword: true, email: $"pw-{Guid.NewGuid():N}@example.com");

        var body = await ResolveAsync(qrId);
        Assert.True(body.Found);
        Assert.True(body.HasPassword);
        // The has-password resolve now also returns the masked on-file email so
        // the app can show it on the password step (was null before the feature).
        Assert.NotNull(body.MaskedEmail);
        Assert.Contains("*", body.MaskedEmail!);
    }

    [Fact]
    public async Task Activation_placeholder_chain_sets_password_and_attaches_email()
    {
        var (userId, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: null);
        var email = $"activated-{Guid.NewGuid():N}@example.com";

        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = qrId, Email = email });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var code = await LatestActivationCodeAsync(userId);
        Assert.NotNull(code);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = qrId, Code = code!, NewPassword = Password, ConfirmPassword = Password,
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.True(await users.HasPasswordAsync(user!));
        Assert.True(await users.CheckPasswordAsync(user!, Password));
        Assert.Equal(email, user!.Email);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task Activation_start_on_account_with_password_returns_409()
    {
        var (_, qrId) = await CreateApprovedVisitorAsync(
            withPassword: true, email: $"pw-{Guid.NewGuid():N}@example.com");

        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = qrId, Email = "x@example.com" });
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        var body = (await start.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BadgeAlreadyActivated, body.Error!.Code);
    }

    [Fact]
    public async Task Activation_complete_with_wrong_code_is_rejected()
    {
        var (userId, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: null);
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = qrId, Email = $"w-{Guid.NewGuid():N}@example.com" });

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = qrId, Code = "000000", NewPassword = Password, ConfirmPassword = Password,
            });
        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.False(await users.HasPasswordAsync(user!));
    }

    [Fact]
    public async Task Activation_wrong_code_leaves_placeholder_email_unchanged_and_badge_still_activatable()
    {
        // Regression (verify-then-attach): a bad code at confirm must NOT have
        // rebound the account email at the start step, so a single typo can never
        // brick / pre-empt a placeholder badge's self-activation.
        var (userId, qrId) = await CreateApprovedVisitorAsync(withPassword: false, email: null);
        var placeholderEmail = await UserEmailAsync(userId);
        Assert.NotNull(placeholderEmail);
        Assert.EndsWith("@simf.local", placeholderEmail!);

        var realEmail = $"real-{Guid.NewGuid():N}@example.com";

        // Start: the holder enters their real email. The server stashes it as
        // pending and must NOT rebind the account email before the code is verified.
        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest { QrId = qrId, Email = realEmail });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        // A wrong code at confirm is rejected — and the placeholder email is
        // untouched (the badge is not bricked and stays activatable).
        var wrong = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = qrId, Code = "000000", NewPassword = Password, ConfirmPassword = Password,
            });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(placeholderEmail, await UserEmailAsync(userId));
        Assert.False(await HasPasswordAsync(userId));

        // The badge is still activatable: the correct code completes and only NOW
        // attaches the entered email.
        var code = await LatestActivationCodeAsync(userId);
        Assert.NotNull(code);
        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = qrId, Code = code!, NewPassword = Password, ConfirmPassword = Password,
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal(realEmail, await UserEmailAsync(userId));
        Assert.True(await HasPasswordAsync(userId));
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string?> UserEmailAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return user?.Email;
    }

    private async Task<bool> HasPasswordAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && await users.HasPasswordAsync(user);
    }

    private async Task<ResolveBadgeResponse> ResolveAsync(string qrId)
    {
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resolve-badge", new ResolveBadgeRequest { QrId = qrId });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<ResolveBadgeResponse>>())!;
        return body.Data!;
    }

    private async Task<string?> LatestActivationCodeAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var hash = await idDb.AccountCodes
            .Where(c => c.UserId == userId
                && c.Purpose == AccountCodePurpose.BadgeActivationOtp
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CodeHash)
            .FirstOrDefaultAsync();
        return hash is null ? null : AuthFlow.RecoverPlaintextCode(hash);
    }

    /// <summary>Creates an Approved visitor with a minted QR id, optionally with
    /// a password, optionally with a real email (null = synthesized placeholder,
    /// mirroring a walk-in registered without one). Returns (userId, qrId).</summary>
    private async Task<(Guid UserId, string QrId)> CreateApprovedVisitorAsync(
        bool withPassword, string? email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // D-819 — the synthesized placeholder here is the BULK-BADGE prefix
        // ("badge-"), not the walk-in one. Both are @simf.local placeholder
        // accounts and this activation chain is unchanged for bulk badges, which
        // are handed out deliberately from a controlled batch.
        //
        // Walk-in-minted badges ("walkin-") are a different case: they circulate
        // openly, so anyone who photographed one could nominate their own email
        // at the start step and claim the account. Activation is refused for
        // those by default — covered by WalkInModeTests.
        var loginEmail = email ?? $"badge-{Guid.NewGuid():N}@simf.local";
        var user = new SimfUser
        {
            UserName = loginEmail,
            Email = loginEmail,
            EmailConfirmed = email is not null,
            DisplayName = "Badge Holder",
            UserType = UserType.Visitor,
            AccountState = AccountState.Approved,
        };
        var create = withPassword
            ? await users.CreateAsync(user, Password)
            : await users.CreateAsync(user);
        Assert.True(create.Succeeded, string.Join(";", create.Errors.Select(e => e.Description)));

        var qrId = NewQrId();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Badge Holder",
            NameArabic = "حامل الشارة",
            QrId = qrId,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return (user.Id, qrId);
    }

    private static string NewQrId()
    {
        // 12-char Crockford-base32 id, derived deterministically from a fresh
        // Guid so each test row is unique (matches the minter's alphabet/length).
        var guid = Guid.NewGuid().ToByteArray();
        var chars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            chars[i] = Alphabet[guid[i] % Alphabet.Length];
        }
        return new string(chars);
    }
}
