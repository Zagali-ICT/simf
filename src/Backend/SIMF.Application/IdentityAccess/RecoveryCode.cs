using System.Security.Cryptography;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Generates, normalises and hashes the user-facing recovery codes that act as
/// a single-use fallback for TOTP. Codes use the Crockford
/// base32 alphabet (no <c>0/O/1/I/L/U</c>) so a code written down by hand is
/// unambiguous to read back. A code is 10 characters of ~50 bits' entropy,
/// printed as <c>XXXXX-XXXXX</c> for readability; the hyphen and any
/// whitespace are stripped on input.
/// </summary>
public static class RecoveryCode
{
    // Crockford base32, minus 'U' (kept out by Crockford himself to dodge
    // accidental obscenity). 30 distinct characters → 30^10 ≈ 5.9e14 ≈ 49 bits.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 10;

    /// <summary>A fresh single recovery code in display form (<c>XXXXX-XXXXX</c>).</summary>
    public static string Generate()
    {
        Span<char> buffer = stackalloc char[CodeLength];
        Span<byte> randoms = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(randoms);
        for (var i = 0; i < CodeLength; i++)
        {
            buffer[i] = Alphabet[randoms[i] % Alphabet.Length];
        }
        return string.Concat(buffer[..5], "-", buffer[5..]);
    }

    /// <summary>
    /// Normalises a code the user typed back — strips whitespace and dashes,
    /// uppercases, so <c>"abcde-fghij"</c>, <c>"ABCDEFGHIJ"</c> and
    /// <c>"ab cde fghij"</c> all hash the same.
    /// </summary>
    public static string Normalise(string raw) =>
        new(raw.Where(character => !char.IsWhiteSpace(character) && character != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    /// <summary>
    /// The persistent hash of a normalised code — SHA-256 hex, same pattern as
    /// <see cref="OpaqueToken.Hash"/>. The plaintext is never stored.
    /// </summary>
    public static string Hash(string normalised) => OpaqueToken.Hash(normalised);
}
