using System.Security.Cryptography;
using System.Text;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Generates and hashes the opaque tokens — refresh tokens and second-factor
/// tickets. The token value is returned to the client once; only its hash is
/// ever stored.
/// </summary>
public static class OpaqueToken
{
    /// <summary>A fresh cryptographically random, URL-safe token value (256-bit).</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>The SHA-256 hash of a token value, as lowercase hex — what gets stored.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
