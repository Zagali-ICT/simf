using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the account-creation endpoints — sign-up, verify-email
/// and resend-code (SIMF-API-001 section 12.4).
/// </summary>
public sealed class RegistrationEndpointsTests : IClassFixture<SimfApiFactory>
{
    private const string ValidPassword = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RegistrationEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@simf.test";

    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = ValidPassword, ConfirmPassword = ValidPassword });

    [Fact]
    public async Task SignUp_returns_201_for_a_new_account()
    {
        var email = NewEmail();

        var response = await SignUpAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignUpResponse>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(email, body.Data!.Email);
    }

    [Fact]
    public async Task SignUp_returns_409_for_a_duplicate_email()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await SignUpAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, body!.Error!.Code);
    }

    [Fact]
    public async Task SignUp_returns_400_for_a_weak_password()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = NewEmail(), Password = "weak", ConfirmPassword = "weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Error!.Code);
    }

    [Fact]
    public async Task VerifyEmail_returns_200_for_the_correct_code()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        var code = GetActiveCode(email);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<VerifyEmailResponse>>();
        Assert.True(body!.Data!.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_returns_400_for_a_wrong_code()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task VerifyEmail_returns_404_for_an_unknown_email()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = NewEmail(), Code = "123456" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendCode_issues_a_new_code_and_invalidates_the_previous_one()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        var firstCode = GetActiveCode(email);

        var resend = await _client.PostAsJsonAsync(
            "/api/v1/auth/resend-code",
            new ResendCodeRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        // The previously issued code no longer verifies.
        var oldCodeResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = firstCode });
        Assert.Equal(HttpStatusCode.BadRequest, oldCodeResponse.StatusCode);
    }

    private string GetActiveCode(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == AccountCodePurpose.EmailVerification
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
    }
}
