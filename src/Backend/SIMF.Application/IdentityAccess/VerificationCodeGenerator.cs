using System.Globalization;
using System.Security.Cryptography;

namespace SIMF.Application.IdentityAccess;

/// <summary>Generates the six-digit numeric verification codes.</summary>
public static class VerificationCodeGenerator
{
    /// <summary>
    /// Returns a fresh six-digit code (<c>000000</c>–<c>999999</c>), drawn from
    /// a cryptographically secure random source.
    /// </summary>
    public static string Generate()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6", CultureInfo.InvariantCulture);
    }
}
