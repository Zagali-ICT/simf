// D-848 — the API minted access tokens its OWN validator refused, because the
// nbf/exp stamps had become a function of the server's OS timezone.
//
// JwtTokenService took "now" from SimfClock (Saudi wall clock, Kind =
// Unspecified). JwtPayload calls ToUniversalTime() on whatever it is handed,
// and on an Unspecified value that means "read this as the HOST's local time".
// The stamped epoch was therefore correct at UTC+03:00 and wrong everywhere
// else: three hours in the future on a UTC host, so every token failed with
// SecurityTokenNotYetValidException against a 30-second ClockSkew; already
// expired east of Riyadh. Sign-in succeeded and then every authorised call
// 401'd, which reached the Control Panel as "the session lasts five seconds".
//
// THE LIMITATION THIS FILE IS BUILT AROUND, stated rather than papered over:
// a test that asserts the minted nbf CANNOT fail on a machine at UTC+03:00 —
// there the buggy conversion and the correct one produce the same instant, and
// no in-process knob changes that (SimfNow composes ToOffset, which preserves
// the instant, so the error is always exactly 3h minus the HOST offset).
// Saudi developers and a Saudi CI runner would both stay green on the defect.
// So the load-bearing ratchet here is Kind, not the instant: see
// The_clock_used_for_token_stamps_is_UTC_kind. The instant assertions below
// are the end-to-end confirmation, and they DO fire anywhere off +03:00.
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class JwtTokenStampTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public JwtTokenStampTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>
    /// THE ratchet. `nbf` / `exp` are epoch seconds a validator compares against
    /// real time, so the clock behind them must be an instant — not a wall-clock
    /// reading that a framework will re-interpret through whatever zone the
    /// server happens to sit in.
    ///
    /// <para>Asserts <see cref="DateTimeKind"/> because that single property
    /// decides whether <c>ToUniversalTime()</c> is a no-op or a host-dependent
    /// shift, and unlike the resulting instant it is the same answer on every
    /// machine. Reverting <c>UtcNowForTokenStamps</c> to <c>SimfNow()</c> fails
    /// this test in Riyadh, in London and in CI alike.</para>
    /// </summary>
    [Fact]
    public void The_clock_used_for_token_stamps_is_UTC_kind()
    {
        var instant = new DateTimeOffset(2026, 8, 4, 20, 17, 56, TimeSpan.Zero);
        var clock = new FakeTimeProvider(instant);

        var stamp = JwtTokenService.UtcNowForTokenStamps(clock);

        Assert.Equal(DateTimeKind.Utc, stamp.Kind);
        Assert.Equal(instant.UtcDateTime, stamp);

        // And the value the defect used is genuinely a different thing — this
        // is what made the bug invisible to review: both are "now", and on a
        // +03:00 host they even convert to the same epoch.
        Assert.Equal(DateTimeKind.Unspecified, clock.SimfNow().Kind);
        Assert.Equal(SimfClock.Offset, clock.SimfNow() - stamp);
    }

    /// <summary>The end-to-end confirmation: a freshly minted token is stamped
    /// at the instant the clock reports, not three hours off it. Passes at
    /// UTC+03:00 either way (see the file header) — it earns its place by
    /// failing everywhere else, which is precisely where production broke.</summary>
    [Fact]
    public async Task A_minted_token_is_stamped_at_the_real_instant()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await CreateUserAsync(scope.ServiceProvider);
        var jwt = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IJwtTokenService>();

        // Against the INJECTED clock, not DateTime.UtcNow: the factory pins a
        // frozen FakeTimeProvider, so real wall time drifts away from it as the
        // suite runs. Comparing to the clock the service was handed is also the
        // stronger claim — it says the stamp IS that instant, exactly.
        var issued = jwt.CreateAccessToken(user, [], [], MobileAppRole.None);
        var token = new JwtSecurityToken(issued.Value);

        // nbf / exp are epoch SECONDS, so truncate before comparing.
        var expected = TruncateToSecond(_factory.Time.GetUtcNow().UtcDateTime);

        Assert.Equal(expected, token.ValidFrom);
        Assert.Equal(expected.AddSeconds(issued.ExpiresInSeconds), token.ValidTo);
    }

    /// <summary>The symptom itself, pinned: the API must accept the token the
    /// API just minted. Nothing in production was subtler than this — sign-in
    /// returned 200 and the very next authorised call returned 401.</summary>
    [Fact]
    public async Task The_API_accepts_a_token_it_has_just_minted()
    {
        using var scope = _factory.Services.CreateScope();
        var user = await CreateUserAsync(scope.ServiceProvider);
        var jwt = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IJwtTokenService>();
        var issued = jwt.CreateAccessToken(user, [], [], MobileAppRole.None);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issued.Value);

        var response = await client.GetAsync("/api/v1/app/users/me");

        // Asserted as OK rather than merely "not 401": the account is Approved
        // and this endpoint is open to any signed-in user, so OK is
        // deterministic — whereas `NotEqual(Unauthorized)` would also pass on a
        // 404 and quietly become a permanent green no-op the day someone
        // renames the route, exactly while the D-848 symptom regressed.
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private static DateTime TruncateToSecond(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Kind);

    private static async Task<SimfUser> CreateUserAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<SimfUser>>();
        var email = $"jwt-stamp-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Jwt Stamp",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user;
    }
}
