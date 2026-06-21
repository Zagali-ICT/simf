// NCA A7-13 — proves the admin-configurable password-expiry gate forces a change
// at sign-in once the password is older than the configured maximum age.
using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PasswordExpiryTests : IClassFixture<PasswordExpiryApiFactory>
{
    private readonly PasswordExpiryApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordExpiryTests(PasswordExpiryApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sign_in_forces_a_change_when_the_password_is_older_than_the_max_age()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        // Move the clock past the 30-day max age so the password is now expired.
        _factory.Time.Advance(TimeSpan.FromDays(31));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthPasswordChangeRequired, body!.Error!.Code);
    }

    [Fact]
    public async Task Sign_in_is_not_expiry_blocked_when_the_password_is_within_the_max_age()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        // Fresh password, well within the 30-day age — sign-in proceeds (a
        // 2FA-enabled visitor returns 200 with an OTP challenge, not a 403).
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
