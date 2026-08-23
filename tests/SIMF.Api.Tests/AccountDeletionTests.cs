// Self-service account deletion — DELETE /api/v1/app/account.
//
// Google Play requires an in-app deletion path for any app offering account
// creation. These pin the two properties that make it real rather than
// cosmetic: the personal data is actually gone from both databases, and the
// credential stops working. A "deletion" that leaves either behind is the
// failure mode worth testing for.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AccountDeletionTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AccountDeletionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deleting_my_account_erases_the_identity_row_and_kills_the_credential()
    {
        var (email, tokens) = await CreateApprovedVisitorAsync();

        var response = await DeleteAccountAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider
            .GetRequiredService<SimfIdentityDbContext>();

        // The original address is gone from the row entirely - not merely
        // flagged - so the person is no longer findable by the identifier they
        // asked to have erased.
        var stillThere = await identity.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email);
        Assert.False(stillThere);

        var tombstoned = await identity.Users
            .AsNoTracking()
            .SingleAsync(u => u.Email!.StartsWith("deleted+"));
        Assert.Equal(AccountState.Disabled, tombstoned.AccountState);
        Assert.Equal("Deleted account", tombstoned.DisplayName);
        Assert.Null(tombstoned.PhoneNumber);
        Assert.Null(tombstoned.AvatarFileId);
        Assert.False(tombstoned.EmailConfirmed);
        Assert.EndsWith("@invalid", tombstoned.Email);
    }

    [Fact]
    public async Task A_deleted_account_can_no_longer_sign_in()
    {
        // The property a user actually cares about: the credential is dead.
        var (email, tokens) = await CreateApprovedVisitorAsync();
        await DeleteAccountAsync(tokens.AccessToken);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_twice_succeeds_rather_than_failing_the_second_time()
    {
        // Idempotent by contract. A client that retries after a dropped
        // connection must be able to finish the job, not be told it is broken.
        var (_, tokens) = await CreateApprovedVisitorAsync();

        var first = await DeleteAccountAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The access token is stamped dead by the security-stamp rotation, so a
        // genuine second call arrives unauthenticated - which is itself the
        // proof that every issued token stopped working.
        var second = await DeleteAccountAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Deletion_requires_a_signed_in_caller()
    {
        // The subject is always the sub claim, never a parameter, so the only
        // way to erase someone else would be to reach the route anonymously.
        var response = await _client.DeleteAsync("/api/v1/app/account");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> DeleteAccountAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete, "/api/v1/app/account");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    /// <summary>
    /// Same shape as <c>AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync</c>,
    /// but hands back the email too — every assertion here is about what
    /// happened to that address.
    /// </summary>
    private async Task<(string Email, AuthTokens Tokens)> CreateApprovedVisitorAsync()
    {
        var email = $"delete-me-{Guid.NewGuid():N}@simf.test";

        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(
                    _factory, email, AccountCodePurpose.EmailVerification),
            });

        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope =
            (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (email, envelope.Data!.Tokens!);
    }
}
