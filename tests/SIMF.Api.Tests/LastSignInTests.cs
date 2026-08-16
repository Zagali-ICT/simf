// NCA A7-31 — proves a completed sign-in records the time and surfaces the prior
// sign-in time to the client (the "last signed in …" notice).
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class LastSignInTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public LastSignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sign_in_reports_the_previous_sign_in_time()
    {
        var firstSignInAt = _factory.Time.SimfNow();

        // First-ever sign-in: there is no previous one.
        var first = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        Assert.Null(first.PreviousSignInAt);

        var email = first.User.Email;
        _factory.Time.Advance(TimeSpan.FromHours(2));

        // Second sign-in surfaces the first sign-in's time as the "previous" notice.
        var second = await SignInAsync(email);
        Assert.NotNull(second.PreviousSignInAt);
        Assert.True(
            Math.Abs((second.PreviousSignInAt!.Value - firstSignInAt).TotalSeconds) < 2,
            $"expected previous ~= {firstSignInAt:o} but was {second.PreviousSignInAt:o}");
    }

    private async Task<AuthTokens> SignInAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!;
    }
}
