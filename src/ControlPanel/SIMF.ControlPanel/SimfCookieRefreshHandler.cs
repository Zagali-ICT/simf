// Tests: SIMF.ControlPanel.Tests/SimfCookieRefreshHandlerTests.cs
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel;

/// <summary>
/// Cookie-validation hook that keeps the bearer token in the auth cookie
/// fresh by exchanging the stored refresh_token for a new token pair just
/// before the access_token expires. Wired into the cookie scheme from
/// <c>Program.cs</c>. A near-identical copy lives in <c>SIMF.Web</c>
/// — the file is duplicated by design, matching the existing
/// SignInTicketStore / AuthEndpoints split between the two host projects.
/// The two have diverged since BUG-005: only this copy carries the
/// cross-request single-flight (see <see cref="Rotations"/>), because the
/// Control Panel is the surface that fires several authenticated fetches per
/// page. The Website copy still rotates once per request.
///
/// <para>Why this exists (D-121): the auth cookie lives 8 hours with sliding
/// renewal but the access token's lifetime is 30 minutes (JwtOptions).
/// Refresh-token rotation has always existed end-to-end on the API and in
/// SimfAuthClient — but nothing in either web project ever called it. So
/// past the 30-minute mark every <c>/account/api/*</c> BFF forwarder read
/// an expired JWT from the cookie and the API returned 401, even though the
/// user's cookie was still valid for hours. The user saw "Authentication is
/// required" on every JS-driven action with no clear remediation.</para>
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

    /// <summary>How long a completed rotation stays replayable to a request that
    /// still carries the OLD refresh token (see <see cref="Rotations"/>). Matched
    /// to <see cref="RefreshThreshold"/>: past it, a cookie presenting a rotated
    /// token is genuinely stale and the API's reuse-detection should see it.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromMinutes(2);

    /// <summary>BUG-005 — cross-request single-flight, keyed by a fingerprint of
    /// the PRESENTED refresh token.
    ///
    /// <para>Every Control Panel page loads its data with several same-origin
    /// authenticated fetches, and this hook runs on each one. Without this map two
    /// requests carrying the same cookie both present the same refresh token: the
    /// first rotates it, the second presents a token the API has already revoked,
    /// and the API treats that as theft — it revokes EVERY session for the account
    /// (SessionService.RefreshAsync) — so an admin in the middle of working is
    /// bounced to /login. The same happens when the browser abandons a request
    /// mid-rotation: the server rotated, the new cookie never landed.</para>
    ///
    /// <para>Holding the rotation here means concurrent (and slightly late)
    /// requests share ONE rotation and all store the same fresh token pair.</para></summary>
    private static readonly ConcurrentDictionary<string, Rotation> Rotations =
        new(StringComparer.Ordinal);

    private sealed record Rotation(
        Lazy<Task<ApiResult<AuthTokens>>> Attempt,
        DateTime StartedAt);

    public static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var properties = context.Properties;
        var accessToken = properties.GetTokenValue("access_token");
        var refreshToken = properties.GetTokenValue("refresh_token");

        // The cookie wasn't carrying API tokens — nothing to rotate.
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        if (!NeedsRefresh(properties.GetTokenValue(ExpiresAtTokenName)))
        {
            return;
        }

        var envelope = await RotateAsync(context, refreshToken);

        if (envelope.Success && envelope.Data is not null)
        {
            StoreTokens(properties, envelope.Data, SimfClock.Now);
            context.ShouldRenew = true;
            return;
        }

        // BUG-005 — the API could not be reached (an unreachable service, a
        // timeout or a proxy error page all surface as INTERNAL_ERROR from
        // SimfAuthClient). That is not a rejection of the session, so keep the
        // principal: the access token is still valid for up to RefreshThreshold
        // and the next request retries. Signing out on a network blip is exactly
        // the mid-work bounce to /login this fix exists to stop.
        if (string.Equals(envelope.Error?.Code, ErrorCodes.InternalError, StringComparison.Ordinal))
        {
            return;
        }

        // The refresh token was revoked, expired, or rejected. Drop the
        // principal so the next request is unauthenticated and the user
        // is sent through /login — the same end state the cookie's own
        // 8-hour expiry would deliver, just reached sooner.
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Runs (or joins) the one rotation for <paramref name="refreshToken"/>
    /// — see <see cref="Rotations"/>. The API call deliberately does NOT take the
    /// request's cancellation token: a browser that abandons the request mid-flight
    /// must not leave the token rotated on the server and un-stored here.</summary>
    private static async Task<ApiResult<AuthTokens>> RotateAsync(
        CookieValidatePrincipalContext context,
        string refreshToken)
    {
        var now = SimfClock.Now;
        PruneExpiredRotations(now);

        var api = context.HttpContext.RequestServices.GetRequiredService<SimfAuthClient>();
        var key = Fingerprint(refreshToken);
        var rotation = Rotations.GetOrAdd(key, _ => new Rotation(
            new Lazy<Task<ApiResult<AuthTokens>>>(() => api.RefreshAsync(
                new RefreshRequest { RefreshToken = refreshToken },
                CancellationToken.None)),
            now));

        var envelope = await rotation.Attempt.Value;
        if (!envelope.Success || envelope.Data is null)
        {
            // Only a SUCCESSFUL rotation is worth replaying. Drop a failed one at
            // once so the next request re-asks the API rather than inheriting a
            // stale verdict for the whole grace window.
            Rotations.TryRemove(key, out _);
        }

        return envelope;
    }

    private static void PruneExpiredRotations(DateTime now)
    {
        foreach (var entry in Rotations)
        {
            if (now - entry.Value.StartedAt > RotationGrace)
            {
                Rotations.TryRemove(entry.Key, out _);
            }
        }
    }

    /// <summary>A SHA-256 fingerprint of the refresh token, so the in-memory
    /// single-flight map is keyed by a digest rather than the secret itself.</summary>
    private static string Fingerprint(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

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
        DateTime issuedAt)
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
        if (!DateTime.TryParse(expiresAtRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt))
        {
            return true;
        }
        return expiresAt - SimfClock.Now <= RefreshThreshold;
    }
}
