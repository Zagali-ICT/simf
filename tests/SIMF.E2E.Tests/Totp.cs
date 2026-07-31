// RFC 6238 TOTP, so the browser suite can complete the Control Panel's second
// factor without shelling out to the PowerShell `Get-Totp` helper the manual
// runs use. Same algorithm, same secret; the secret itself always comes from the
// environment (SIMF_QA_ADMIN_TOTP_SECRET) and is never written down here.
using System.Security.Cryptography;

namespace SIMF.E2E.Tests;

public static class Totp
{
    /// <summary>The current 6-digit code for a base32 secret.
    ///
    /// <para>RFC 6238 counts 30-second steps from the UNIX EPOCH. That is a
    /// cryptographic counter, not a user-facing date, so it stays UTC even though
    /// the rest of SIMF is Saudi-local: localising it would change every generated
    /// code and break 2FA outright.</para></summary>
    public static string Now(string base32Secret, DateTimeOffset? at = null)
    {
        var key = FromBase32(base32Secret);
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;

        var beCounter = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(beCounter);
        }

        var hash = HMACSHA1.HashData(key, beCounter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] FromBase32(string input)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.TrimEnd('=').Replace(" ", "").ToUpperInvariant();

        var bits = 0;
        var value = 0;
        var output = new List<byte>(cleaned.Length * 5 / 8);
        foreach (var c in cleaned)
        {
            var index = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"'{c}' is not a base32 character.");
            }
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
