// Emailed one-time-code confirmation on self-service account deletion.
// Exercises the real gate (AccountDeletion:RequireCodeForDeletion ON): erasing
// is refused without a fresh code, the send endpoint issues one, a valid code
// erases and burns it, a wrong or expired one erases NOTHING, the code is
// capped per window, and - the point of the whole design - a pending or
// disabled holder can still complete deletion.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Account;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Runs the API with <c>AccountDeletion:RequireCodeForDeletion</c> ON,
/// so the real gate is exercised end-to-end. The base factory leaves it OFF, so
/// <c>AccountDeletionTests</c> keeps driving the erase path on its own.</summary>
public sealed class AccountDeletionCodeApiFactory : SimfApiFactory
{
    public AccountDeletionCodeApiFactory() =>
        Environment.SetEnvironmentVariable(
            "AccountDeletion__RequireCodeForDeletion", "true");
}

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AccountDeletionCodeTests
    : IClassFixture<AccountDeletionCodeApiFactory>
{
    private const string SendCode = "/api/v1/app/account/delete/send-code";
    private const string Delete = "/api/v1/app/account";

    private readonly AccountDeletionCodeApiFactory _factory;
    private readonly HttpClient _client;

    public AccountDeletionCodeTests(AccountDeletionCodeApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deleting_without_a_code_is_refused_and_erases_nothing()
    {
        var (token, userId, email) = await CreateVisitorAsync();

        var response = await DeleteAsync(token, code: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AccountDeletionCodeRequired, body.Error!.Code);
        // The assertion that matters: validation runs BEFORE the first mutation,
        // so a refused call leaves the account whole rather than half-scrubbed.
        await AssertStillExistsAsync(userId, email);
    }

    [Fact]
    public async Task The_refusals_answer_403_and_never_401()
    {
        // Load-bearing, not stylistic. On a 401 the mobile client refreshes and
        // REPLAYS the same request (simf_api_client.dart, _execute), so one
        // mistyped code would burn two of the five attempts - and a failed
        // refresh signs the holder out in the middle of deleting.
        var (token, _, _) = await CreateVisitorAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(token, null)).StatusCode);

        await SendCodeAsync(token);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await DeleteAsync(token, "000000")).StatusCode);
    }

    [Fact]
    public async Task Sending_a_code_returns_the_masked_address_and_stores_one_row()
    {
        var (token, userId, _) = await CreateVisitorAsync();

        var result = await SendCodeAsync(token);

        Assert.StartsWith("d", result.MaskedEmail, StringComparison.Ordinal);
        Assert.Contains("***@simf.test", result.MaskedEmail, StringComparison.Ordinal);
        Assert.Equal(600, result.ExpiresInSeconds);
        Assert.Equal(1, await CodeCountAsync(userId));
    }

    [Fact]
    public async Task A_valid_code_erases_the_account_and_burns_the_code()
    {
        var (token, userId, _) = await CreateVisitorAsync();
        var code = await IssueKnownCodeAsync(userId);

        var response = await DeleteAsync(token, code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.SingleAsync(u => u.Id == userId);
        Assert.StartsWith("deleted+", user.Email!, StringComparison.Ordinal);
        var stored = await identity.Set<AccountCode>()
            .SingleAsync(c => c.UserId == userId
                && c.Purpose == AccountCodePurpose.AccountDeletion);
        Assert.NotNull(stored.ConsumedAt);
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_erases_nothing()
    {
        var (token, userId, email) = await CreateVisitorAsync();
        await IssueKnownCodeAsync(userId);

        var response = await DeleteAsync(token, "999999");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AccountDeletionCodeInvalid, body.Error!.Code);
        await AssertStillExistsAsync(userId, email);
    }

    [Fact]
    public async Task An_expired_code_is_refused_with_its_own_code()
    {
        // Distinct from Invalid on purpose: the app zeroes its resend countdown
        // on Expired so the user can ask for a new code straight away, instead
        // of reading "wrong code" while a timer runs down.
        var (token, userId, email) = await CreateVisitorAsync();
        var code = await IssueKnownCodeAsync(userId, expiresInMinutes: -1);

        var response = await DeleteAsync(token, code);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AccountDeletionCodeExpired, body.Error!.Code);
        await AssertStillExistsAsync(userId, email);
    }

    [Fact]
    public async Task Five_wrong_attempts_burn_the_code()
    {
        var (token, userId, email) = await CreateVisitorAsync();
        await IssueKnownCodeAsync(userId);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await DeleteAsync(token, "111111");
        }

        // Burned, so the sixth try reads as "no code at all" rather than "wrong".
        var response = await DeleteAsync(token, "111111");
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AccountDeletionCodeRequired, body.Error!.Code);
        await AssertStillExistsAsync(userId, email);
    }

    [Fact]
    public async Task Requesting_a_second_code_retires_the_first()
    {
        // Driven through the real send path, because that is where "only the
        // newest stays valid" lives. Seeding two rows directly would prove
        // nothing: the test clock does not advance, so both would carry the
        // same CreatedAt and the ordering would be arbitrary.
        var (token, userId, _) = await CreateVisitorAsync();

        await SendCodeAsync(token);
        await SendCodeAsync(token);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var codes = await identity.Set<AccountCode>()
            .Where(c => c.UserId == userId
                && c.Purpose == AccountCodePurpose.AccountDeletion)
            .ToListAsync();

        Assert.Equal(2, codes.Count);
        Assert.Equal(1, codes.Count(c => c.ConsumedAt is null));
    }

    [Theory]
    [InlineData(AccountState.PendingApproval)]
    [InlineData(AccountState.Rejected)]
    [InlineData(AccountState.Disabled)]
    public async Task A_holder_who_is_not_approved_can_still_request_and_delete(
        AccountState state)
    {
        // The guard against a future "align this with the biometric step-up".
        // That endpoint demands an approved account and refuses a disabled one;
        // copying either would lock out precisely the people deletion exists
        // for, which is what AccountDeleteEndpoint's own remarks say.
        var (token, userId, _) = await CreateVisitorAsync();
        await SetAccountStateAsync(userId, state);

        var sent = await _client.SendAsync(Authorized(HttpMethod.Post, SendCode, token));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        // The send above issued a code whose plaintext this test cannot read.
        // Clear it before seeding a known one: the test TimeProvider does not
        // advance, so two rows would share a CreatedAt and "latest unconsumed"
        // would be a coin toss. Production has no such tie - SimfNow moves.
        await ClearCodesAsync(userId);
        var code = await IssueKnownCodeAsync(userId);
        var response = await DeleteAsync(token, code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_biometric_step_up_code_cannot_delete_an_account()
    {
        // Purpose scoping: same plaintext, wrong purpose, no erasure.
        var (token, userId, email) = await CreateVisitorAsync();
        await IssueKnownCodeAsync(
            userId, purpose: AccountCodePurpose.BiometricEnrolStepUp);

        var response = await DeleteAsync(token, "123456");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AccountDeletionCodeRequired, body.Error!.Code);
        await AssertStillExistsAsync(userId, email);
    }

    [Fact]
    public async Task Sending_is_capped_per_window()
    {
        var (token, _, _) = await CreateVisitorAsync();

        for (var request = 0; request < 5; request++)
        {
            await SendCodeAsync(token);
        }

        var sixth = await _client.SendAsync(Authorized(HttpMethod.Post, SendCode, token));
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        var body = (await sixth.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RateLimitExceeded, body.Error!.Code);
    }

    // ----- helpers -----------------------------------------------------------

    private static HttpRequestMessage Authorized(
        HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private Task<HttpResponseMessage> DeleteAsync(string token, string? code)
    {
        var request = Authorized(HttpMethod.Delete, Delete, token);
        request.Content = JsonContent.Create(new DeleteAccountRequest { Code = code });
        return _client.SendAsync(request);
    }

    private async Task<SendAccountDeletionCodeResponse> SendCodeAsync(string token)
    {
        var response = await _client.SendAsync(
            Authorized(HttpMethod.Post, SendCode, token));
        response.EnsureSuccessStatusCode();
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SendAccountDeletionCodeResponse>>())!;
        return body.Data!;
    }

    /// <summary>Writes a code straight into the store, so a test knows the
    /// plaintext without reading the outbox.</summary>
    private async Task<string> IssueKnownCodeAsync(
        Guid userId,
        string plaintext = "123456",
        int expiresInMinutes = 10,
        AccountCodePurpose purpose = AccountCodePurpose.AccountDeletion)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var now = _factory.Time.SimfNow();
        identity.Set<AccountCode>().Add(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(expiresInMinutes),
        });
        await identity.SaveChangesAsync();
        return plaintext;
    }

    /// <summary>Drops every deletion code for one account, so a test can seed a
    /// single row it knows the plaintext of.</summary>
    private async Task ClearCodesAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        await identity.Set<AccountCode>()
            .Where(c => c.UserId == userId
                && c.Purpose == AccountCodePurpose.AccountDeletion)
            .ExecuteDeleteAsync();
    }

    private async Task<int> CodeCountAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await identity.Set<AccountCode>()
            .CountAsync(c => c.UserId == userId
                && c.Purpose == AccountCodePurpose.AccountDeletion);
    }

    private async Task AssertStillExistsAsync(Guid userId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(email, user.Email);
        Assert.NotEqual(AccountState.Disabled, user.AccountState);
    }

    private async Task SetAccountStateAsync(Guid userId, AccountState state)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.SingleAsync(u => u.Id == userId);
        user.AccountState = state;
        await identity.SaveChangesAsync();
    }

    private async Task<(string accessToken, Guid userId, string email)>
        CreateVisitorAsync()
    {
        var email = $"del-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Deletion Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (envelope.Data!.Tokens!.AccessToken, userId, email);
    }
}
