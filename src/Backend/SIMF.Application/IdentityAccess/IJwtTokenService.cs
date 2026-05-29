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
    /// Flutter app reads it directly without an extra round-trip.</summary>
    AccessToken CreateAccessToken(
        SimfUser user, IEnumerable<string> roles,
        SIMF.Common.Enums.MobileAppRole mobileAppRole);
}
