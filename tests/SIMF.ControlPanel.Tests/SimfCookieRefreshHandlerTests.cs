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
                Value = DateTimeOffset.UtcNow.Add(expiresIn ?? TimeSpan.FromSeconds(30))
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
