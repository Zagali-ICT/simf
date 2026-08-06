using System.Security.Cryptography;
using System.Text;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Keyed HMAC of a short verification code, so the
/// <c>AccountCodes</c> table stores a hash, not the live OTP. The six-digit
/// code space is only 10^6, so a plain (keyless) hash is brute-forced offline
/// in milliseconds; the HMAC key — a server secret that never lives in the DB —
/// is what makes a leaked table useless. Truncated to 16 lowercase-hex chars to
/// fit the frozen <c>AccountCode.Code</c> column; 64 bits of a keyed MAC
/// is ample for a single-use, ~10-minute, attempt-capped code.
/// </summary>
public static class AccountCodeHasher
{
    // Deterministic dev fallback so a code can always be hashed even before
    // ConfigureKey runs (tests / DI-only setups). Production overrides this with
    // the configured server secret via ConfigureKey.
    private static byte[] _key =
        SHA256.HashData(Encoding.UTF8.GetBytes("simf-account-code-dev"));

    /// <summary>Install the HMAC key once at startup (from DI). Reuses the JWT
    /// signing key, which is already a required, boot-validated server secret.
    /// A null/empty key leaves the dev fallback in place.</summary>
    public static void ConfigureKey(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _key = Encoding.UTF8.GetBytes(key);
        }
    }

    /// <summary>The keyed-HMAC hash of a verification code — the value stored in
    /// <c>AccountCode.Code</c> and compared on redemption. Lowercase hex, 16 chars.</summary>
    public static string Hash(string? code) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code ?? string.Empty)))[..16];
}
