using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// The expired-reset-code test is isolated in its own fixture: it advances the
/// test clock, which would future-date tokens issued by other tests sharing the
/// same factory.
/// </summary>
public sealed class PasswordResetExpiryTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetExpiryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reset_password_with_an_expired_code_returns_400()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        var code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);

        // The reset code lives 10 minutes — move past it.
        _factory.Time.Advance(TimeSpan.FromMinutes(11));

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = "NewPassw0rd!",
                ConfirmPassword = "NewPassw0rd!",
            });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var body = await reset.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthResetCodeExpired, body!.Error!.Code);
    }
}
