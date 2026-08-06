namespace SIMF.Common.Badges;

/// <summary>
/// Crockford base32 for the offline event badge payload.
///
/// <para>Base32 rather than base64, for three reasons that all matter on a
/// printed badge:</para>
/// <list type="number">
/// <item>QR codes have an ALPHANUMERIC mode covering uppercase letters and
/// digits that packs 5.5 bits per character, against 8 bits per character in
/// byte mode. Base64 needs lowercase, which forces byte mode — so base64
/// produces fewer characters but a physically LARGER QR. Base32 in
/// alphanumeric mode is roughly 20% smaller as a printed code.</item>
/// <item><c>QrId.Normalise</c> upper-cases every scanned value before it is
/// resolved. Base64 would be corrupted by that; base32 is unaffected.</item>
/// <item>Crockford's alphabet excludes the look-alike characters I, L, O and U,
/// so a damaged badge can be re-keyed by hand at the desk without transcription
/// errors.</item>
/// </list>
///
/// <para>NOTE: this is the canonical 32-character Crockford alphabet, which is
/// deliberately NOT the same as the 30-character alphabet
/// <c>QrIdMinter</c> uses for the 12-character serial (that one also drops 0
/// and 1). An encoding needs exactly 32 symbols; the minter's alphabet is a
/// human-facing id alphabet. Do not conflate them.</para>
/// </summary>
public static class CrockfordBase32
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Encodes a single 5-bit value as one character. Used for the badge's
    /// leading key-version character, which is a bare 5-bit field rather than a
    /// byte stream — running it through <see cref="Encode"/> would pad it to a
    /// full byte and emit two characters.
    /// </summary>
    public static char EncodeSymbol(int value)
    {
        if (value is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), "A base32 symbol carries 5 bits (0..31).");
        }
        return Alphabet[value];
    }

    /// <summary>Reads a single character back to its 5-bit value, applying the
    /// same case-folding and look-alike folding as <see cref="TryDecode"/>.</summary>
    public static bool TryDecodeSymbol(char symbol, out int value)
    {
        var folded = char.ToUpperInvariant(symbol) switch
        {
            'I' or 'L' => '1',
            'O' => '0',
            var other => other,
        };

        value = Alphabet.IndexOf(folded, StringComparison.Ordinal);
        return value >= 0;
    }

    /// <summary>Encodes bytes as Crockford base32, most-significant bit first.
    /// No padding: the decoder derives the byte count from the length.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) { return string.Empty; }

        var output = new System.Text.StringBuilder((bytes.Length * 8 / 5) + 1);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var current in bytes)
        {
            buffer = (buffer << 8) | current;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                output.Append(Alphabet[(buffer >> bitsInBuffer) & 0x1F]);
            }
        }

        // Flush the tail, left-aligned in its final 5-bit group.
        if (bitsInBuffer > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return output.ToString();
    }

    /// <summary>
    /// Decodes Crockford base32. Case-insensitive, and applies Crockford's
    /// look-alike folding (I and L read as 1, O reads as 0) so a hand-keyed
    /// badge still resolves. Returns false on any character outside the
    /// alphabet rather than throwing: a malformed scan is an ordinary denial,
    /// not an exception path.
    /// </summary>
    public static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value)) { return false; }

        var output = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var raw in value)
        {
            var symbol = char.ToUpperInvariant(raw);
            symbol = symbol switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => symbol,
            };

            var index = Alphabet.IndexOf(symbol, StringComparison.Ordinal);
            if (index < 0) { return false; }

            buffer = (buffer << 5) | index;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((buffer >> bitsInBuffer) & 0xFF));
            }
        }

        bytes = [.. output];
        return true;
    }
}
