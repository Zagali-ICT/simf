using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>A signed JWT access token and how long it is valid for.</summary>
public sealed record AccessToken(string Value, int ExpiresInSeconds);

/// <summary>Issues the JWT access token for a signed-in user.</summary>
public interface IJwtTokenService
{
    /// <summary>D-161 — the <paramref name="mobileAppRole"/> resolved
    /// per <see cref="IUserProfileService.ResolveMobileAppRoleAsync"/>
    /// is carried on the JWT as the <c>mobile_app_role</c> claim so the
    /// Flutter app reads it directly without an extra round-trip.
    /// <para>Issue-1 — <paramref name="permissions"/> are the resolved
    /// permission codes (or the single wildcard for an Administrator),
    /// minted as <c>perm</c> claims so the authorization handler can gate
    /// per page-and-action without a per-request database lookup.</para></summary>
    AccessToken CreateAccessToken(
        SimfUser user, IEnumerable<string> roles, IEnumerable<string> permissions,
        SIMF.Common.Enums.MobileAppRole mobileAppRole);

    /// <summary>P3.2b — D-232 (D-213): mints a short-lived token scoped to ONE
    /// session recording, for the range-streaming endpoint. Distinct audience
    /// (<c>Jwt:StreamAudience</c>) + a <c>recording_session_id</c> claim — it
    /// carries no roles / permissions / security-stamp, so it cannot be replayed
    /// against any other endpoint. Lifetime is <c>Jwt:StreamTokenMinutes</c>.</summary>
    AccessToken CreateRecordingStreamToken(Guid sessionId);
}
