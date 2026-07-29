// Tests: SIMF.Api.Tests/SessionRecordingTests.cs (P3.2b — D-232 recording-stream token)
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

/// <summary>Issues HMAC-SHA256-signed JWT access tokens (SIMF-API-001 section 12.2).</summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)    : IJwtTokenService
{
    /// <summary>D-443 — the absolute session lifetime read from
    /// <c>Jwt:SessionLifetimeHours</c> (default 24h).</summary>
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromHours(options.Value.SessionLifetimeHours);

    public AccessToken CreateAccessToken(
        SimfUser user, IEnumerable<string> roles, IEnumerable<string> permissions,
        MobileAppRole mobileAppRole, bool? secondFactorCompleted = null)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("display_name", user.DisplayName),
            // The security stamp lets a sensitive endpoint detect a revoked
            // session before the token expires (SIMF-FDS-001 Amendment A.6).
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            // P10 — D-051: account_state + user_type travel in every token
            // so an authorization handler (P11) can route a non-approved
            // user to the pending / rejected page without an extra round
            // trip, and the client can switch surfaces (CP vs App) by
            // user_type alone.
            new("account_state", user.AccountState.ToString()),
            new("user_type", user.UserType.ToString()),
            // D-161 — the resolved mobile-app role the Flutter app uses to
            // route screens / show or hide gate-operator surfaces.
            new("mobile_app_role", mobileAppRole.ToString()),
            // Held-item #2c — RFC 8176 `amr`. "mfa" means this token cleared a
            // second factor (TOTP, recovery code or emailed OTP); "pwd" means it
            // was minted on the password alone, which SignInService only does for
            // an account with 2FA turned off. Before this claim existed the two
            // were indistinguishable downstream, so no policy could require the
            // stronger one. Derived from TwoFactorEnabled when the caller does not
            // state it — see IJwtTokenService for why that derivation is sound.
            new("amr", (secondFactorCompleted ?? user.TwoFactorEnabled) ? "mfa" : "pwd"),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Issue-1 + D-563 — the resolved permission codes (or the single wildcard
        // "*" for an Administrator) UNION the operational perms the user's
        // MobileAppRole confers, minted as `perm` claims so the authorization
        // handler can gate per page-and-action without a per-request DB lookup
        // (PermissionCatalog.ClaimType). App Staff / Moderator attendees gain
        // gate-scan + walk-in + moderation purely from their ProfileType's
        // MobileAppRole — never an admin RBAC role (option A) — so the same
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
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            settings.AccessTokenMinutes * 60);
    }

    public AccessToken CreateRecordingStreamToken(Guid sessionId, Guid userId)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
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
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            settings.StreamTokenMinutes * 60);
    }
}
