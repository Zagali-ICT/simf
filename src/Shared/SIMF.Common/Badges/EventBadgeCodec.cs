using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SIMF.Common.Badges;

/// <summary>
/// D-819 — the offline event badge payload: a profile-type number and a
/// sequence number, encrypted so a gate can validate the badge with NO network.
/// </summary>
/// <param name="ProfileTypeCode">
/// <c>UserProfileType.Code</c>. Travels inside the badge so an offline gate can
/// check it against that gate's allowed-profile-type list without a lookup.
/// </param>
/// <param name="Sequence">
/// The badge serial. Each offline desk is assigned a range when its key is
/// provisioned (desk 3 issues 3000001 upward), so disconnected desks can mint
/// badges simultaneously with no coordination and no collisions.
/// </param>
public readonly record struct EventBadgePayload(int ProfileTypeCode, long Sequence);

/// <summary>
/// D-819 — encodes and decodes the offline event badge.
///
/// <para>Wire format: <c>{keyVersion}{base32(nonce || ciphertext || tag)}</c>,
/// where the plaintext is the ASCII string <c>"{profileTypeCode},{sequence}"</c>.
/// Two plain numbers separated by a comma is what keeps the code small enough to
/// stay readable when a badge is creased or the lighting is poor.</para>
///
/// <para>ENCRYPTED, not merely signed. AES-GCM's authentication tag means a
/// successful decrypt IS the validation: a badge that decrypts to a well-formed
/// payload is genuine, and one that does not is rejected. One shared symmetric
/// key covers the desk generator, the scanners and the server, which is what
/// lets a scanner verify with no network.</para>
///
/// <para>The leading key-version character is outside the ciphertext so a key
/// can be rotated with an overlap window: badges issued under the previous key
/// keep validating while the new key is rolled out.</para>
///
/// <para>SECURITY NOTE: a shared symmetric key means an extracted scanner key
/// could mint valid badges. That is an accepted trade (the owner asked for one
/// key both sides use); the compensating controls are key rotation via the
/// version character and full reconciliation of every scan after the event. An
/// asymmetric scheme would remove the risk at the cost of a larger payload.</para>
///
/// <para>Sizes: <see cref="AesGcm"/> requires a 12-byte nonce, and D-820 takes
/// the full 16-byte tag (see <see cref="TagBytes"/>). Overhead is therefore 28
/// bytes on a 9-byte payload, and a typical badge is about 61 characters — which
/// is why <c>GateScans.QrIdAtScan</c> is nvarchar(96).</para>
/// </summary>
public static class EventBadgeCodec
{
    /// <summary>AES-GCM nonce length. Fixed at 12 by <see cref="AesGcm.NonceByteSizes"/>.</summary>
    public const int NonceBytes = 12;

    /// <summary>
    /// AES-GCM tag length. The FULL 16 bytes.
    ///
    /// <para>D-820 corrected this from the 12-byte .NET minimum, which was
    /// chosen purely to shrink the printed QR. The Flutter scanner decrypts
    /// badges with <c>pointycastle</c>, whose GCM implementation accepts only a
    /// 128-bit tag — a 12-byte tag is unreadable on the device that has to read
    /// it. The alternatives were hand-rolling truncated-tag GCM in Dart, which
    /// is the worst possible place for clever cryptography, or giving up offline
    /// verification. A full tag is also the stronger choice; the cost is about
    /// seven more characters on the badge, which is why
    /// <c>GateScans.QrIdAtScan</c> moved to nvarchar(96).</para>
    /// </summary>
    public const int TagBytes = 16;

    /// <summary>Required key length: AES-256.</summary>
    public const int KeyBytes = 32;

    /// <summary>Upper bound on an accepted scan, so a hostile or garbled input
    /// cannot push work into the decoder. A real badge is ~61 characters.</summary>
    public const int MaxEncodedLength = 128;

    /// <summary>
    /// Builds the badge string. <paramref name="keyVersion"/> is emitted as a
    /// single leading Crockford character, so versions 0..31 are available.
    /// </summary>
    public static string Encode(
        EventBadgePayload payload, ReadOnlySpan<byte> key, int keyVersion)
    {
        if (payload.ProfileTypeCode < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload), "The profile-type code cannot be negative.");
        }
        if (payload.Sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload), "The badge sequence cannot be negative.");
        }
        if (key.Length != KeyBytes)
        {
            throw new ArgumentException(
                $"The badge key must be {KeyBytes} bytes (AES-256).", nameof(key));
        }
        if (keyVersion is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyVersion), "The key version must be 0..31.");
        }

        var plaintext = Encoding.ASCII.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{payload.ProfileTypeCode},{payload.Sequence}"));

        var blob = new byte[NonceBytes + plaintext.Length + TagBytes];
        var nonce = blob.AsSpan(0, NonceBytes);
        var ciphertext = blob.AsSpan(NonceBytes, plaintext.Length);
        var tag = blob.AsSpan(NonceBytes + plaintext.Length, TagBytes);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return CrockfordBase32.EncodeSymbol(keyVersion) + CrockfordBase32.Encode(blob);
    }

    /// <summary>
    /// Reads the key version from a badge WITHOUT decrypting, so the caller can
    /// select the right key (current or previous) during a rotation window.
    /// </summary>
    public static bool TryReadKeyVersion(string? encoded, out int keyVersion)
    {
        keyVersion = -1;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedLength)
        {
            return false;
        }
        return CrockfordBase32.TryDecodeSymbol(encoded[0], out keyVersion);
    }

    /// <summary>
    /// Validates and decodes a badge. Returns false for anything that is not a
    /// genuine badge under <paramref name="key"/> — wrong key, tampered
    /// payload, truncated scan, garbage input. A failed decode is an ordinary
    /// denial, so this never throws on bad input.
    /// </summary>
    public static bool TryDecode(
        string? encoded, ReadOnlySpan<byte> key, out EventBadgePayload payload)
    {
        payload = default;

        if (string.IsNullOrEmpty(encoded)
            || encoded.Length > MaxEncodedLength
            || key.Length != KeyBytes)
        {
            return false;
        }

        // Strip the key-version character; the rest is the AES-GCM blob.
        if (!CrockfordBase32.TryDecode(encoded[1..], out var blob)
            || blob.Length <= NonceBytes + TagBytes)
        {
            return false;
        }

        var plaintextLength = blob.Length - NonceBytes - TagBytes;
        var plaintext = new byte[plaintextLength];

        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(
                blob.AsSpan(0, NonceBytes),
                blob.AsSpan(NonceBytes, plaintextLength),
                blob.AsSpan(NonceBytes + plaintextLength, TagBytes),
                plaintext);
        }
        catch (CryptographicException)
        {
            // Authentication failed: wrong key, or the payload was altered.
            return false;
        }

        return TryParsePlaintext(plaintext, out payload);
    }

    private static bool TryParsePlaintext(
        ReadOnlySpan<byte> plaintext, out EventBadgePayload payload)
    {
        payload = default;

        Span<char> chars = stackalloc char[plaintext.Length];
        for (var i = 0; i < plaintext.Length; i++)
        {
            // The plaintext we write is ASCII digits and one comma. Anything
            // else means a successful decrypt of a payload we did not author.
            if (plaintext[i] > 0x7F) { return false; }
            chars[i] = (char)plaintext[i];
        }

        var separator = chars.IndexOf(',');
        if (separator <= 0 || separator == chars.Length - 1) { return false; }

        if (!int.TryParse(
                chars[..separator], NumberStyles.None,
                CultureInfo.InvariantCulture, out var profileTypeCode)
            || !long.TryParse(
                chars[(separator + 1)..], NumberStyles.None,
                CultureInfo.InvariantCulture, out var sequence))
        {
            return false;
        }

        payload = new EventBadgePayload(profileTypeCode, sequence);
        return true;
    }
}
