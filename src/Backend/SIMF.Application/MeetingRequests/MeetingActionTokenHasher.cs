using System.Security.Cryptography;
using System.Text;

namespace SIMF.Application.MeetingRequests;

/// <summary>
/// D-717 (item 7, FDS-013 §15.7 GAP-3) — mints and hashes the speaker
/// action-link token secret, so the <c>MeetingActionTokens</c> table stores a
/// keyed-HMAC hash, not the live secret (a leaked table cannot forge a link). The
/// secret is a 256-bit random value carried only in the emailed URL; the HMAC-SHA256
/// digest is stored full-length (64 hex chars) — unlike the 6-digit OTP hasher there
/// is no truncation, because the token space is already astronomically large.
/// Mirrors <see cref="IdentityAccess.AccountCodeHasher"/> and reuses the JWT signing
/// key as the HMAC key.
/// </summary>
public static class MeetingActionTokenHasher
{
    // Deterministic dev fallback so a token can be hashed before ConfigureKey runs
    // (tests / DI-only setups). Production overrides it with the configured secret.
    private static byte[] _key =
        SHA256.HashData(Encoding.UTF8.GetBytes("simf-meeting-action-token-dev"));

    /// <summary>Install the HMAC key once at startup (from DI). A null/empty key
    /// leaves the dev fallback in place.</summary>
    public static void ConfigureKey(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _key = Encoding.UTF8.GetBytes(key);
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
