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
}
