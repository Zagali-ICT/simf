// Tests: SIMF.Web.Tests/SimfCookieRefreshHandlerTests.cs (todo).
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Web;

/// <summary>
/// Cookie-validation hook that keeps the bearer token in the auth cookie
/// fresh by exchanging the stored refresh_token for a new token pair just
/// before the access_token expires. Wired into the cookie scheme from
/// <c>Program.cs</c>. A near-identical copy lives in <c>SIMF.ControlPanel</c>
/// — the file is duplicated by design, matching the existing
/// SignInTicketStore / AuthEndpoints split between the two host projects.
///
/// <para>Why this exists (D-121): the auth cookie lives 8 hours with sliding
/// renewal but the access token's lifetime is 30 minutes (JwtOptions).
/// Refresh-token rotation has always existed end-to-end on the API and in
/// SimfAuthClient — but nothing in either web project ever called it. So
/// past the 30-minute mark every <c>/account/api/*</c> BFF forwarder read
/// an expired JWT from the cookie and the API returned 401, even though the
/// user's cookie was still valid for hours.</para>
///
/// <para>Centralising the refresh in <see cref="CookieAuthenticationEvents.OnValidatePrincipal"/>
/// means the rotation runs once per request, transparently, ahead of every
/// BFF call — so there is no per-endpoint retry plumbing to maintain.</para>
/// </summary>
public static class SimfCookieRefreshHandler
{
    /// <summary>Name of the stored token holding the access_token expiry
    /// (round-trip ISO-8601 UTC). Written at sign-in and on every successful
    /// refresh; read on every cookie-validate.</summary>
    public const string ExpiresAtTokenName = "expires_at";

    /// <summary>Refresh when the access token has this little life remaining
    /// (or has already expired). Chosen to comfortably exceed the JWT
    /// validator's 30-second clock-skew tolerance so a refresh always lands
    /// well inside the still-valid window — see <c>JwtBearerSetup.cs</c>.</summary>
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);

    public static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var properties = context.Properties;
        var accessToken = properties.GetTokenValue("access_token");
        var refreshToken = properties.GetTokenValue("refresh_token");

        // The cookie wasn't carrying API tokens — nothing to rotate. This
        // happens on the public-anonymous parts of the Website where the
        // cookie may exist for culture/state without an authenticated user.
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        if (!NeedsRefresh(properties.GetTokenValue(ExpiresAtTokenName)))
        {
            return;
        }

        var api = context.HttpContext.RequestServices.GetRequiredService<SimfAuthClient>();
        ApiResult<AuthTokens> envelope;
        try
        {
            envelope = await api.RefreshAsync(
                new RefreshRequest { RefreshToken = refreshToken },
                context.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
            when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The request was abandoned mid-validation. Leave the principal
            // intact — the user will retry naturally.
            return;
        }

        if (!envelope.Success || envelope.Data is null)
        {
            // The refresh token was revoked, expired, or rejected. Drop the
            // principal so the next request is unauthenticated and the user
            // is sent through /login — the same end state the cookie's own
            // 8-hour expiry would deliver, just reached sooner.
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        StoreTokens(properties, envelope.Data, DateTimeOffset.UtcNow);
        context.ShouldRenew = true;
    }

    /// <summary>
    /// Persists the access_token / refresh_token / expires_at trio onto the
    /// supplied properties. Called from <c>AuthEndpoints.cs</c> at sign-in
    /// so the very first cookie write already carries expires_at — without
    /// it, <see cref="OnValidatePrincipalAsync"/> would refresh on the very
    /// next request.
    /// </summary>
    public static void StoreTokens(
        AuthenticationProperties properties,
        AuthTokens tokens,
        DateTimeOffset issuedAt)
    {
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken },
            new AuthenticationToken
            {
                Name = ExpiresAtTokenName,
                Value = issuedAt.AddSeconds(tokens.AccessTokenExpiresInSeconds)
                    .ToString("O", CultureInfo.InvariantCulture),
            },
        ]);
    }

    private static bool NeedsRefresh(string? expiresAtRaw)
    {
        // Without a stored expiry (e.g. a cookie minted before D-121 lands)
        // the safest move is to refresh immediately — the alternative is
        // waiting for the next BFF call to 401, which is exactly the bug
        // this hook exists to fix.
        if (string.IsNullOrEmpty(expiresAtRaw)) return true;
        if (!DateTimeOffset.TryParse(expiresAtRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt))
        {
            return true;
        }
        return expiresAt - DateTimeOffset.UtcNow <= RefreshThreshold;
    }
}
