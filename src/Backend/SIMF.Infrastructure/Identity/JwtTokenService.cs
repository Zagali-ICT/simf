using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SIMF.Application.IdentityAccess;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>Issues HMAC-SHA256-signed JWT access tokens (SIMF-API-001 section 12.2).</summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IJwtTokenService
{
    public AccessToken CreateAccessToken(SimfUser user, IEnumerable<string> roles)
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
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

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
}
