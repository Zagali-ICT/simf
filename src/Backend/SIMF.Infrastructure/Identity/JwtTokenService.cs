// Tests: SIMF.Api.Tests/JwtTokenStampTests.cs
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>Issues HMAC-SHA256-signed JWT access tokens.</summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)    : IJwtTokenService
{
    /// <summary>The absolute session lifetime read from
    /// <c>Jwt:SessionLifetimeHours</c> (default 24h).</summary>
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromHours(options.Value.SessionLifetimeHours);

    public AccessToken CreateAccessToken(
        SimfUser user, IEnumerable<string> roles, IEnumerable<string> permissions,
        MobileAppRole mobileAppRole, bool? secondFactorCompleted = null)
    {
        var settings = options.Value;
        var now = UtcNowForTokenStamps(timeProvider);
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("display_name", user.DisplayName),
            // The security stamp lets a sensitive endpoint detect a revoked
            // session before the token expires.
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            // The account_state and user_type claims travel in every token
            // so an authorization handler can route a non-approved
            // user to the pending / rejected page without an extra round
            // trip, and the client can switch surfaces (CP vs App) by
            // user_type alone.
            new("account_state", user.AccountState.ToString()),
            new("user_type", user.UserType.ToString()),
            // The resolved mobile-app role the Flutter app uses to
            // route screens / show or hide gate-operator surfaces.
            new("mobile_app_role", mobileAppRole.ToString()),
            // RFC 8176 `amr`. "mfa" means this token cleared a
            // second factor (TOTP, recovery code or emailed OTP); "pwd" means it
            // was minted on the password alone, which SignInService only does for
            // an account with 2FA turned off. Before this claim existed the two
            // were indistinguishable downstream, so no policy could require the
            // stronger one. Derived from TwoFactorEnabled when the caller does not
            // state it — see IJwtTokenService for why that derivation is sound.
            new("amr", (secondFactorCompleted ?? user.TwoFactorEnabled) ? "mfa" : "pwd"),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // The resolved permission codes (or the single wildcard
        // "*" for an Administrator) UNION the operational perms the user's
        // MobileAppRole confers, minted as `perm` claims so the authorization
        // handler can gate per page-and-action without a per-request DB lookup
        // (PermissionCatalog.ClaimType). App Staff / Moderator attendees gain
        // gate-scan + walk-in + moderation purely from their ProfileType's
        // MobileAppRole — never an admin RBAC role — so the same
        // permission gate authorises both the CP desk and the staff app screen.
        var effectivePermissions = permissions
            .Concat(PermissionCatalog.OperationalPermissionsForAppRole(mobileAppRole))
            .Distinct(StringComparer.Ordinal);
        claims.AddRange(effectivePermissions.Select(code => new Claim(PermissionCatalog.ClaimType, code)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            settings.AccessTokenMinutes * 60);
    }

    public AccessToken CreateRecordingStreamToken(Guid sessionId, Guid userId)
    {
        var settings = options.Value;
        var now = UtcNowForTokenStamps(timeProvider);
        var expires = now.AddMinutes(settings.StreamTokenMinutes);

        // Deliberately minimal: NO roles, NO perm claims, NO security_stamp —
        // a stream token authorises one recording and nothing else. The
        // distinct audience pins it to the StreamToken scheme.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("recording_session_id", sessionId.ToString()),
            // L1 (security) — bind the token to the requesting user so a leaked
            // stream URL is attributable. The StreamToken scheme still skips the
            // security-stamp check on the hot streaming path.
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.StreamAudience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            settings.StreamTokenMinutes * 60);
    }

    /// <summary>
    /// "now" for the <c>nbf</c> / <c>exp</c> stamps, deliberately in UTC
    /// rather than through <see cref="SimfClockExtensions.SimfNow"/>.
    ///
    /// <para>This is the same exemption the 2026-07-31 Saudi-local cutover
    /// granted TOTP and <c>CookieOptions.Expires</c>: <c>nbf</c> and <c>exp</c>
    /// are epoch seconds that a validator compares against real time, not
    /// wall-clock readings anyone displays. Handing <c>JwtSecurityToken</c> a
    /// Saudi reading with <see cref="DateTimeKind.Unspecified"/> is NOT
    /// harmless, because <c>JwtPayload</c> calls <c>ToUniversalTime()</c> on it
    /// — and on an Unspecified value that means "interpret this as the HOST
    /// OS's local time". The stamped epoch therefore silently became a function
    /// of the server's timezone: correct only at UTC+03:00, three hours in the
    /// future on a UTC host (every token rejected
    /// <c>SecurityTokenNotYetValidException</c>), already expired east of
    /// Riyadh. The API would mint a token and its own validator would refuse
    /// it a moment later — with a 30-second <c>ClockSkew</c> nowhere near wide
    /// enough to absorb the gap.</para>
    ///
    /// <para>A <see cref="DateTimeKind.Utc"/> value makes <c>ToUniversalTime()</c>
    /// a no-op, so no local-zone conversion can be applied and the result is
    /// identical on every host — which is exactly the host-independence
    /// <see cref="SimfClock"/> was written to guarantee and that this one call
    /// site was quietly undoing. Nothing user-facing is affected: no
    /// timestamp here is ever displayed, and the wire contract is unchanged
    /// (these claims were always epoch seconds).</para>
    ///
    /// <para><b>internal, not private, on purpose.</b> The <c>Kind</c> this
    /// returns is the entire defect: <c>Utc</c> makes the conversion a no-op,
    /// <c>Unspecified</c> invites the host-local one. Asserting the minted
    /// <c>nbf</c> instead cannot catch a regression on a developer or CI
    /// machine that happens to sit at UTC+03:00 — there the two are
    /// indistinguishable by construction. Asserting the Kind holds on every
    /// host, so the seam is exposed to let the ratchet be real.</para>
    /// </summary>
    internal static DateTime UtcNowForTokenStamps(TimeProvider timeProvider) =>
        timeProvider.GetUtcNow().UtcDateTime;
}
