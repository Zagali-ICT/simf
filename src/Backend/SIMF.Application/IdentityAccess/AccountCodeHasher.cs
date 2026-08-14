// Tests: SIMF.Application.Tests/AccountCodeHasherTests.cs (deterministic hash,
//        brute-force recovery of the plaintext, and per-purpose key separation).
using System.Security.Cryptography;
using System.Text;
using SIMF.Application.Security;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Keyed HMAC of a short verification code, so the
/// <c>AccountCodes</c> table stores a hash, not the live OTP. The six-digit
/// code space is only 10^6, so a plain (keyless) hash is brute-forced offline
/// in milliseconds; the HMAC key — a server secret that never lives in the DB —
/// is what makes a leaked table useless. Truncated to 16 lowercase-hex chars to
/// match <c>AccountCodeConfiguration</c>'s <c>HasMaxLength(16)</c> and the
/// <c>nvarchar(16)</c> column: 64 bits of a keyed MAC is ample for a single-use,
/// ~10-minute, attempt-capped code. The width is a deliberate choice rather than
/// an inherited freeze, but it is still coupled — widening it means editing this
/// truncation, the entity configuration and the migration together.
/// </summary>
public static class AccountCodeHasher
{
    // Deterministic dev fallback so a code can always be hashed even before
    // ConfigureKey runs (tests / DI-only setups). Production overrides this with
    // the configured server secret via ConfigureKey.
    private static byte[] _key =
        PurposeKey.Derive("simf-account-code-dev", PurposeKey.AccountCodeLabel);

    /// <summary>Install the HMAC key once at startup (from DI). The master secret is
    /// the JWT signing key, already a required, boot-validated server secret; it is
    /// never used as the HMAC key directly, so this hasher shares no key material
    /// with the speaker action-link hasher and neither can be attacked with the
    /// other's stored hashes. The limit is worth stating: the JWT signature still
    /// uses the master value itself, so derivation contains a weakness in one hash
    /// from reaching the other — it protects nothing if the master leaks.
    /// A null/empty key leaves the dev fallback in place.</summary>
    public static void ConfigureKey(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _key = PurposeKey.Derive(key, PurposeKey.AccountCodeLabel);
        }
    }

    /// <summary>The keyed-HMAC hash of a verification code — the value stored in
    /// <c>AccountCode.CodeHash</c> and compared on redemption. Lowercase hex, 16 chars.</summary>
    public static string Hash(string? code) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code ?? string.Empty)))[..16];
}
