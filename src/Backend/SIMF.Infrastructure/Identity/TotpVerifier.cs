// Tests: SIMF.Application.Tests/IdentityAccess/TotpVerifierTests.cs
using OtpNet;
using SIMF.Application.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Verifies an authenticator-app TOTP code (RFC 6238) against the user's
/// base32-encoded secret, allowing a one-step clock-skew window each way, and
/// reports the matched time-step so a replay can be detected.
/// </summary>
/// <remarks>
/// The secret is normalised before Base32 decoding — whitespace is removed and
/// the characters are uppercased — so a key copy-pasted with the human-readable
/// spaces that authenticator apps display (for example
/// <c>"abcd efgh ijkl mnop"</c>) decodes correctly. Diagnosing myComment #35
/// (2026-05-23): the seeded key contained spaces, which made
/// <c>Base32Encoding.ToBytes</c> throw and the verifier silently fail-closed,
/// so every code looked wrong.
/// </remarks>
internal sealed class TotpVerifier : ITotpVerifier
{
    public TotpResult Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return new TotpResult(false, 0);
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(Normalise(secret));
        }
        catch (ArgumentException)
        {
            // A malformed stored secret cannot verify anything — fail closed.
            return new TotpResult(false, 0);
        }

        var isValid = new Totp(secretBytes).VerifyTotp(
            code, out var matchedStep, new VerificationWindow(previous: 1, future: 1));
        return new TotpResult(isValid, matchedStep);
    }

    /// <summary>
    /// Strips whitespace and uppercases the secret so a key with the spaces an
    /// authenticator app shows (<c>"abcd efgh …"</c>) or in lower case still
    /// decodes as Base32.
    /// </summary>
    private static string Normalise(string secret) =>
        new string(secret
            .Where(character => !char.IsWhiteSpace(character))
            .Select(char.ToUpperInvariant)
            .ToArray());
}
