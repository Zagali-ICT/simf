using System.Net;
using System.Net.Http.Json;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// H7 — D-062: the "auth-email" rate-limit policy keys its partition on
/// the normalised email in the request body, so an attacker who rotates
/// source IPs against ONE account is bounded by the per-email cap before
/// the per-account lockout fires (which would be a DoS against the
/// legitimate user).
///
/// <para>The test factory leaves the per-IP cap permissive (100 000) and
/// tightens the per-email cap to 3 so a fourth attempt against the same
/// email returns 429 even though every request comes from the same test
/// IP — proving the partitions are independent.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class EmailRateLimitTests : IClassFixture<EmailRateLimitedApiFactory>
{
    private readonly HttpClient _client;

    public EmailRateLimitTests(EmailRateLimitedApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sign_in_against_the_same_email_trips_the_per_email_cap()
    {
        var email = $"rl-email-{Guid.NewGuid():N}@simf.test";

        // The first three attempts pass the per-email gate (no such account
        // exists, so they each return 401 — but the rate limiter has let
        // them through to the endpoint). The fourth attempt is rejected by
        // the limiter with 429.
        var first = await SignInAsync(email);
        var second = await SignInAsync(email);
        var third = await SignInAsync(email);
        var fourth = await SignInAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }

    [Fact]
    public async Task A_different_email_has_its_own_per_email_partition()
    {
        var victim = $"rl-email-victim-{Guid.NewGuid():N}@simf.test";
        var bystander = $"rl-email-bystander-{Guid.NewGuid():N}@simf.test";

        // Trip the victim's per-email cap with three attempts.
        await SignInAsync(victim);
        await SignInAsync(victim);
        await SignInAsync(victim);
        var victimRejected = await SignInAsync(victim);
        Assert.Equal(HttpStatusCode.TooManyRequests, victimRejected.StatusCode);

        // The bystander email has its own partition — must still be admitted.
        var bystanderAttempt = await SignInAsync(bystander);
        Assert.Equal(HttpStatusCode.Unauthorized, bystanderAttempt.StatusCode);
    }

    private Task<HttpResponseMessage> SignInAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = "Zx9#mKp2!" });
}
