using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>A signed JWT access token and how long it is valid for.</summary>
public sealed record AccessToken(string Value, int ExpiresInSeconds);

/// <summary>Issues the JWT access token for a signed-in user.</summary>
public interface IJwtTokenService
{
    /// <summary>The <paramref name="mobileAppRole"/> resolved
    /// per <see cref="IUserProfileService.ResolveMobileAppRoleAsync"/>
    /// is carried on the JWT as the <c>mobile_app_role</c> claim so the
    /// Flutter app reads it directly without an extra round-trip.
    /// <para>Issue-1 — <paramref name="permissions"/> are the resolved
    /// permission codes (or the single wildcard for an Administrator),
    /// minted as <c>perm</c> claims so the authorization handler can gate
    /// per page-and-action without a per-request database lookup.</para></summary>
    /// <para>Held-item #2c — <paramref name="secondFactorCompleted"/> drives the
    /// <c>amr</c> claim (<c>mfa</c> vs <c>pwd</c>), so an authorization policy can
    /// tell a token that actually cleared a second factor from one minted on a
    /// password alone. Without it every token looks identical to the authorization
    /// layer and a "require MFA" policy is impossible to express.</para>
    /// <para>Pass <c>null</c> to DERIVE it from <c>user.TwoFactorEnabled</c>. That
    /// is sound rather than a guess: SignInService issues tokens without a second
    /// factor only on the <c>!TwoFactorEnabled</c> fast path, so for an enrolled
    /// account a token can only exist if a second factor was cleared. Refresh and
    /// device-key issuance use the derivation because neither re-runs the
    /// challenge itself.</para>
    AccessToken CreateAccessToken(
        SimfUser user, IEnumerable<string> roles, IEnumerable<string> permissions,
        SIMF.Common.Enums.MobileAppRole mobileAppRole,
        bool? secondFactorCompleted = null);

    /// <summary>Mints a short-lived token scoped to ONE
    /// session recording, for the range-streaming endpoint. Distinct audience
    /// (<c>Jwt:StreamAudience</c>) + a <c>recording_session_id</c> claim — it
    /// carries no roles / permissions / security-stamp, so it cannot be replayed
    /// against any other endpoint. L1 (security): also carries the requesting
    /// user's <c>sub</c> so a leaked token is attributable. Lifetime is
    /// <c>Jwt:StreamTokenMinutes</c>.</summary>
    AccessToken CreateRecordingStreamToken(Guid sessionId, Guid userId);

    /// <summary>D-443 (NCA finding): the absolute session lifetime
    /// (<c>Jwt:SessionLifetimeHours</c>, default 24h) — how long after sign-in
    /// the refresh-token chain stays valid. The issue sites stamp a new token's
    /// <c>ExpiresAt</c> from this; rotation carries that deadline forward rather
    /// than extending it, so the session is capped at this span from sign-in.</summary>
    TimeSpan RefreshTokenLifetime { get; }
}
