// Tests: SIMF.Api.Tests/DelegationMeetingActionTokenTests.cs (mint, redeem,
//        expiry) and SIMF.Application.Tests/AccountCodeHasherTests.cs (this
//        hasher's key is derived, and differs from the account-code one).
using System.Security.Cryptography;
using System.Text;
using SIMF.Application.Security;

namespace SIMF.Application.MeetingRequests;

/// <summary>
/// Mints and hashes the speaker
/// action-link token secret, so the <c>MeetingActionTokens</c> table stores a
/// keyed-HMAC hash, not the live secret (a leaked table cannot forge a link). The
/// secret is a 256-bit random value carried only in the emailed URL; the HMAC-SHA256
/// digest is stored full-length (64 hex chars) — unlike the 6-digit OTP hasher there
/// is no truncation, because the token space is already astronomically large.
/// Mirrors <see cref="IdentityAccess.AccountCodeHasher"/>, including deriving its
/// HMAC key from the JWT signing key rather than using that secret directly.
/// </summary>
public static class MeetingActionTokenHasher
{
    // Deterministic dev fallback so a token can be hashed before ConfigureKey runs
    // (tests / DI-only setups). Production overrides it with the configured secret.
    private static byte[] _key =
        PurposeKey.Derive("simf-meeting-action-token-dev", PurposeKey.MeetingActionLabel);

    /// <summary>Install the HMAC key once at startup (from DI). The master secret is
    /// the JWT signing key; a per-purpose subkey is derived from it rather than using
    /// it as the HMAC key, so this hasher and the account-code hasher hold different
    /// keys despite being configured from one value. A null/empty key leaves the dev
    /// fallback in place.</summary>
    public static void ConfigureKey(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _key = PurposeKey.Derive(key, PurposeKey.MeetingActionLabel);
        }
    }

    /// <summary>A fresh URL-safe, high-entropy token secret (32 random bytes,
    /// base64url). This is what goes into the email URL; only its hash is stored.</summary>
    public static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The keyed-HMAC-SHA256 hash of a token secret — the value stored in
    /// <c>MeetingActionToken.TokenHash</c> and compared on redemption. Lowercase
    /// hex, 64 chars.</summary>
    public static string Hash(string? secret) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(secret ?? string.Empty)));
}
