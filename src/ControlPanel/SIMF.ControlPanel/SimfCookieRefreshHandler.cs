// Tests: SIMF.ControlPanel.Tests/SimfCookieRefreshHandlerTests.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SIMF.ControlPanel;

/// <summary>
/// Cookie-validation hook that swaps the stored refresh_token for a fresh token
/// pair just before the access_token expires. Wired up in <c>Program.cs</c>.
/// The auth cookie lives 8 hours but the access token only 5 minutes
/// (Jwt:AccessTokenMinutes, NCA cap D-443), so without this every
/// <c>/account/api/*</c> BFF call 401s on a still-valid cookie.
/// </summary>
public static class SimfCookieRefreshHandler
{
    /// <summary>Stored-token name for the access_token expiry (zoned ISO-8601).</summary>
    public const string ExpiresAtTokenName = "expires_at";

    /// <summary>Refresh with this much life left. Well clear of the JWT
    /// validator's 30-second clock skew.</summary>
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);

    /// <summary>How long a completed rotation stays replayable to a request
    /// still holding the old token.</summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromMinutes(2);

    /// <summary>Cross-request single-flight, keyed by a hash of the presented
    /// refresh token. A page's concurrent fetches would otherwise each present
    /// the same token, and the API reads the second one as theft and revokes
    /// every session for the account.</summary>
    private static readonly ConcurrentDictionary<string, Rotation> Rotations =
        new(StringComparer.Ordinal);

    private sealed record Rotation(Lazy<Task<ApiResult<AuthTokens>>> Attempt, DateTime StartedAt);

    public static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        AuthenticationProperties properties = context.Properties;
        var accessToken = properties.GetTokenValue("access_token");
        var refreshToken = properties.GetTokenValue("refresh_token");

        // No API tokens in the cookie, so nothing to rotate.
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        if (!NeedsRefresh(properties.GetTokenValue(ExpiresAtTokenName)))
        {
            return;
        }

        ApiResult<AuthTokens> envelope = await RotateAsync(context, refreshToken);

        if (envelope.Success && envelope.Data is not null)
        {
            StoreTokens(properties, envelope.Data, SimfClock.Now);
            context.ShouldRenew = true;
            return;
        }

        // API unreachable, not a rejected session: keep the principal, the token
        // is good for another RefreshThreshold and the next request retries.
        if (string.Equals(envelope.Error?.Code, ErrorCodes.InternalError, StringComparison.Ordinal))
        {
            return;
        }

        // The refresh token was revoked, expired or rejected: sign out.
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Runs or joins the single rotation for this token. Passes
    /// CancellationToken.None so an abandoned request cannot rotate the token
    /// server-side without storing the result.</summary>
    private static async Task<ApiResult<AuthTokens>> RotateAsync(CookieValidatePrincipalContext context, string refreshToken)
    {
        DateTime now = SimfClock.Now;
        PruneExpiredRotations(now);

        SimfAuthClient api = context.HttpContext.RequestServices.GetRequiredService<SimfAuthClient>();
        var key = Fingerprint(refreshToken);
        Rotation rotation = Rotations.GetOrAdd(key, _ => new Rotation(
            new Lazy<Task<ApiResult<AuthTokens>>>(() => api.RefreshAsync(
                new RefreshRequest { RefreshToken = refreshToken },
                CancellationToken.None)),
            now));

        ApiResult<AuthTokens> envelope = await rotation.Attempt.Value;
        if (!envelope.Success || envelope.Data is null)
        {
            // Only a successful rotation is worth replaying.
            _ = Rotations.TryRemove(key, out _);
        }

        return envelope;
    }

    private static void PruneExpiredRotations(DateTime now)
    {
        foreach (KeyValuePair<string, Rotation> entry in Rotations)
        {
            if (now - entry.Value.StartedAt > RotationGrace)
            {
                _ = Rotations.TryRemove(entry.Key, out _);
            }
        }
    }

    /// <summary>Keys the map by a digest rather than the secret itself.</summary>
    private static string Fingerprint(string refreshToken)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    /// <summary>Writes the access_token / refresh_token / expires_at trio. Also
    /// called at sign-in from <c>AuthEndpoints.cs</c>, so the first cookie
    /// already carries expires_at.</summary>
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
                // The offset must be named: /session/status hands this string to
                // the browser, where `new Date(...)` reads an offset-less value
                // as browser-local time. SpecifyKind first, because the
                // DateTimeOffset ctor throws on a Utc or Local Kind that
                // disagrees with the offset given.
                Value = new DateTimeOffset(
                        DateTime.SpecifyKind(
                            issuedAt.AddSeconds(tokens.AccessTokenExpiresInSeconds),
                            DateTimeKind.Unspecified),
                        SimfClock.Offset)
                    .ToString("O", CultureInfo.InvariantCulture),
            },
        ]);
    }

    private static bool NeedsRefresh(string? expiresAtRaw)
    {
        // No expiry stored, so refresh now.
        if (string.IsNullOrEmpty(expiresAtRaw))
        {
            return true;
        }

        // A legacy value carrying no offset would parse as this host's zone.
        if (!HasZoneDesignator(expiresAtRaw))
        {
            return true;
        }

        return !DateTimeOffset.TryParse(expiresAtRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset expiresAt) || expiresAt - DateTimeOffset.Now <= RefreshThreshold;
    }

    /// <summary>True when the timestamp names its zone ("Z" or "+03:00").</summary>
    private static bool HasZoneDesignator(string value)
    {
        if (value.EndsWith('Z') || value.EndsWith('z'))
        {
            return true;
        }

        // Measured from the END, so the date's own '-' separators are never read
        // as the offset's sign.
        var sign = value.LastIndexOfAny(['+', '-']);
        return sign > 0 && value.Length - sign == 6 && value[sign + 3] == ':';
    }
}
