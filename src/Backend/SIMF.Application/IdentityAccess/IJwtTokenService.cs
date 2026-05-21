using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>A signed JWT access token and how long it is valid for.</summary>
public sealed record AccessToken(string Value, int ExpiresInSeconds);

/// <summary>Issues the JWT access token for a signed-in user.</summary>
public interface IJwtTokenService
{
    AccessToken CreateAccessToken(SimfUser user, IEnumerable<string> roles);
}
