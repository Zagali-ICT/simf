// NCA A7-20 — proves a change/reset cannot reuse the current password or a recent
// retired one when password history is enabled.
using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PasswordHistoryTests : IClassFixture<PasswordHistoryApiFactory>
{
    private const string Fresh1 = "Fresh1#Aa9x";
    private const string Fresh2 = "Fresh2#Bb8y";

    private readonly PasswordHistoryApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordHistoryTests(PasswordHistoryApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reset_to_a_previously_used_password_is_rejected()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        // Reset to a fresh password — the original password is retired into history.
        var first = await ResetToAsync(email, Fresh1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Resetting BACK to the original (now in history) is rejected as reuse.
        var reused = await ResetToAsync(email, AuthFlow.Password);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        var body = await reused.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthPasswordPolicy, body!.Error!.Code);

        // A brand-new password still works.
        var third = await ResetToAsync(email, Fresh2);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    [Fact]
    public async Task Reset_to_the_current_password_is_rejected()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        // Move the current password to Fresh1, then try to reset to Fresh1 again.
        Assert.Equal(HttpStatusCode.OK, (await ResetToAsync(email, Fresh1)).StatusCode);

        var sameAgain = await ResetToAsync(email, Fresh1);
        Assert.Equal(HttpStatusCode.BadRequest, sameAgain.StatusCode);
        var body = await sameAgain.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthPasswordPolicy, body!.Error!.Code);
    }

    private async Task<HttpResponseMessage> ResetToAsync(string email, string newPassword)
    {
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        var code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);
        return await _client.PostAsJsonAsync(
            "/api/v1/app/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            });
    }
}
