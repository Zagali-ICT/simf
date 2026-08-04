using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Tests;

/// <summary>BUG-005 — the cookie-validate refresh hook. A Control Panel page
/// loads its data with several same-origin authenticated fetches, so this hook
/// runs concurrently on requests carrying the SAME cookie. Two of them presenting
/// the same refresh token made the API's rotation reuse-detection revoke every
/// session for the account, bouncing a working admin to /login; and a transient
/// transport failure signed the user out outright. These pin both.</summary>
public sealed class SimfCookieRefreshHandlerTests
{
    private const string AccessToken = "stale.access.token";

    [Fact]
    public async Task Concurrent_requests_on_one_cookie_rotate_the_refresh_token_once()
    {
        // BUG-005 regression — without the single-flight the second request
        // presents an already-rotated token and the API kills every session.
        var refreshToken = NewRefreshToken();
        using var handler = new RefreshApiStub();
        var services = BuildServices(handler);

        var first = NewContext(services, refreshToken);
        var second = NewContext(services, refreshToken);

        var firstCall = SimfCookieRefreshHandler.OnValidatePrincipalAsync(first);
        var secondCall = SimfCookieRefreshHandler.OnValidatePrincipalAsync(second);
        await handler.WaitUntilCalledAsync();
        handler.Release();
        await Task.WhenAll(firstCall, secondCall);

        Assert.Equal(1, handler.Calls);
        Assert.True(first.ShouldRenew);
        Assert.True(second.ShouldRenew);
        Assert.Equal("rotated.refresh.token", TokenOf(first, "refresh_token"));
        Assert.Equal("rotated.refresh.token", TokenOf(second, "refresh_token"));
        Assert.Equal("rotated.access.token", TokenOf(first, "access_token"));
        Assert.NotNull(first.Principal);
        Assert.NotNull(second.Principal);
    }

    [Fact]
    public async Task A_request_arriving_after_the_rotation_reuses_its_result()
    {
        // The browser abandoning a request mid-rotation used to leave the server
        // rotated and the cookie stale — the next request then tripped the same
        // reuse-detection. The completed rotation is replayed instead.
        var refreshToken = NewRefreshToken();
        using var handler = new RefreshApiStub();
        var services = BuildServices(handler);
        handler.Release();

        var first = NewContext(services, refreshToken);
        await SimfCookieRefreshHandler.OnValidatePrincipalAsync(first);

        var late = NewContext(services, refreshToken);
        await SimfCookieRefreshHandler.OnValidatePrincipalAsync(late);

        Assert.Equal(1, handler.Calls);
        Assert.True(late.ShouldRenew);
        Assert.Equal("rotated.refresh.token", TokenOf(late, "refresh_token"));
    }

    [Fact]
    public async Task An_unreachable_api_keeps_the_principal_instead_of_signing_out()
    {
        // A network blip is not a rejected session: the access token is still
        // valid for up to the refresh threshold and the next request retries.
        var refreshToken = NewRefreshToken();
        using var handler = new RefreshApiStub { Unreachable = true };
        var services = BuildServices(handler);
        handler.Release();

        var context = NewContext(services, refreshToken);
        await SimfCookieRefreshHandler.OnValidatePrincipalAsync(context);

        Assert.NotNull(context.Principal);
        Assert.False(context.ShouldRenew);
        Assert.Equal(refreshToken, TokenOf(context, "refresh_token"));
    }

    [Fact]
    public async Task A_cookie_whose_access_token_is_still_fresh_is_left_alone()
    {
        using var handler = new RefreshApiStub();
        var services = BuildServices(handler);
        handler.Release();

        var context = NewContext(services, NewRefreshToken(), expiresIn: TimeSpan.FromMinutes(4));
        await SimfCookieRefreshHandler.OnValidatePrincipalAsync(context);

        Assert.Equal(0, handler.Calls);
        Assert.False(context.ShouldRenew);
    }

    /// <summary>D-848 — the stored expiry must name its zone.
    ///
    /// <para><c>/session/status</c> hands this string straight to the browser,
    /// where <c>new Date(...)</c> reads an offset-less ISO date-time as the
    /// BROWSER's local time. Written from <see cref="SimfClock"/> (Kind
    /// Unspecified), <c>"O"</c> emitted no designator at all — so how long a
    /// Control Panel session appeared to last depended on the timezone of the
    /// admin's laptop, and a browser far enough east of Riyadh saw an expiry
    /// already in the past and was signed out on the guard's first tick.</para></summary>
    [Fact]
    public void The_stored_expiry_names_its_offset_so_a_browser_cannot_reinterpret_it()
    {
        var properties = new AuthenticationProperties();
        var issuedAt = new DateTime(2026, 8, 4, 23, 17, 56, DateTimeKind.Unspecified);

        SimfCookieRefreshHandler.StoreTokens(
            properties,
            new AuthTokens(
                AccessToken,
                "refresh-zoned",
                "Bearer",
                AccessTokenExpiresInSeconds: 300,
                new AuthUser(Guid.NewGuid(), "admin@simf.test", "Admin")),
            issuedAt);

        var stored = properties.GetTokenValue(SimfCookieRefreshHandler.ExpiresAtTokenName);

        Assert.NotNull(stored);
        Assert.EndsWith("+03:00", stored, StringComparison.Ordinal);

        // And it round-trips to the intended instant rather than to whatever
        // the reader's own zone would have made of a bare reading.
        Assert.True(DateTimeOffset.TryParse(
            stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed));
        Assert.Equal(
            new DateTimeOffset(issuedAt.AddSeconds(300), SimfClock.Offset),
            parsed);
    }

    /// <summary>A cookie minted BEFORE D-848 carries no offset. Parsing it would
    /// silently assume the CP host's zone, so it is treated as due instead —
    /// one extra rotation on the next request, never a wrong answer.</summary>
    [Fact]
    public async Task A_legacy_zone_free_expiry_refreshes_rather_than_being_guessed()
    {
        using var handler = new RefreshApiStub();
        var services = BuildServices(handler);
        handler.Release();

        var context = NewContext(services, NewRefreshToken(), expiresIn: TimeSpan.FromMinutes(4));
        // Overwrite with the pre-D-848 shape: same instant, no designator.
        context.Properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = NewRefreshToken() },
            new AuthenticationToken
            {
                Name = SimfCookieRefreshHandler.ExpiresAtTokenName,
                Value = SimfClock.Now.AddMinutes(4).ToString("O", CultureInfo.InvariantCulture),
            },
        ]);

        await SimfCookieRefreshHandler.OnValidatePrincipalAsync(context);

        // Four minutes of life left, so a ZONED value would have been left alone
        // (see A_cookie_whose_access_token_is_still_fresh_is_left_alone).
        Assert.Equal(1, handler.Calls);
        Assert.EndsWith(
            "+03:00",
            TokenOf(context, SimfCookieRefreshHandler.ExpiresAtTokenName)!,
            StringComparison.Ordinal);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>A fresh token per test — the single-flight map is static and
    /// keyed by the presented refresh token, so tests must not share one.</summary>
    private static string NewRefreshToken() => $"refresh-{Guid.NewGuid():N}";

    private static ServiceProvider BuildServices(RefreshApiStub handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new SimfAuthClient(new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.simf.test/"),
        }));
        return services.BuildServiceProvider();
    }

    private static CookieValidatePrincipalContext NewContext(
        IServiceProvider services,
        string refreshToken,
        TimeSpan? expiresIn = null)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken
            {
                Name = SimfCookieRefreshHandler.ExpiresAtTokenName,
                // D-848 — zoned, exactly as StoreTokens writes it. A zone-free
                // value is now treated as a pre-D-848 legacy cookie and always
                // refreshes, so writing one here would silently stop these
                // tests exercising the freshness decision at all.
                Value = new DateTimeOffset(
                        SimfClock.Now.Add(expiresIn ?? TimeSpan.FromSeconds(30)),
                        SimfClock.Offset)
                    .ToString("O", CultureInfo.InvariantCulture),
            },
        ]);

        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(CookieAuthenticationHandler));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@simf.test")], "TestCookie"));
        var ticket = new AuthenticationTicket(principal, properties, scheme.Name);
        var http = new DefaultHttpContext { RequestServices = services };

        return new CookieValidatePrincipalContext(
            http, scheme, new CookieAuthenticationOptions(), ticket);
    }

    private static string? TokenOf(CookieValidatePrincipalContext context, string name) =>
        context.Properties.GetTokenValue(name);

    /// <summary>Stands in for the API's <c>/auth/refresh</c>. Counts the calls and
    /// holds them open so a test can prove two overlapping validations share one.</summary>
    private sealed class RefreshApiStub : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        /// <summary>When set, the call fails the way an unreachable API does.</summary>
        public bool Unreachable { get; init; }

        public int Calls => Volatile.Read(ref _calls);

        public void Release() => _release.TrySetResult();

        public Task WaitUntilCalledAsync() => _entered.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _entered.TrySetResult();
            await _release.Task;

            if (Unreachable)
            {
                throw new HttpRequestException("The API could not be reached.");
            }

            var tokens = new AuthTokens(
                "rotated.access.token",
                "rotated.refresh.token",
                "Bearer",
                300,
                new AuthUser(Guid.NewGuid(), "admin@simf.test", "Administrator"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(ApiResult<AuthTokens>.Ok(tokens)),
            };
        }
    }
}
