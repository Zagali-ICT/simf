using System.Globalization;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text;

namespace SIMF.Common.Badges;

/// <summary>
/// The event badge payload, encrypted so a gate can validate the badge with NO
/// network. It carries the three questions a scanner has to answer on its own:
/// who is this, is the badge from the open year, and is this tier admitted here.
/// </summary>
/// <param name="ProfileId">WHO — the attendee record, which every attendee has
/// with or without an app account. Sixteen raw bytes; the same value written as
/// 32 hexadecimal characters would push the printed code past the gate's
/// 96-character ceiling on its own.</param>
/// <param name="EditionYear">WHEN — the edition the badge was issued for, so a
/// device can refuse last year's badge without asking anything.</param>
/// <param name="ProfileTypeCode">WHAT — the tier, so a device can decide whether
/// this type is admitted at THIS gate.</param>
public readonly record struct EventBadgePayload(
    Guid ProfileId, int EditionYear, int ProfileTypeCode);

/// <summary>
/// Encodes and decodes the offline event badge.
///
/// <para>Wire format: <c>{keyVersion}{base32(nonce || ciphertext || tag)}</c>,
/// where the plaintext is 20 RAW BYTES — a 16-byte profile id, a 2-byte edition
/// year and a 2-byte profile-type code, all big-endian.</para>
///
/// <para>Raw bytes rather than text is a size decision, not a taste one. The
/// profile id written as 32 hexadecimal characters would exceed the gate's
/// 96-character ceiling on its own, and anything over that ceiling is refused as
/// "not recognised" BEFORE it is ever decrypted — which at a desk with a queue is
/// undiagnosable. Packed as bytes the whole badge is 78 characters.</para>
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
/// <para>Sizes: <see cref="AesGcm"/> requires a 12-byte nonce, and this codec
/// takes the full 16-byte tag (see <see cref="TagBytes"/>). Overhead is therefore 28
/// bytes on the 20-byte payload, so every badge is EXACTLY 78 characters: 48 bytes
/// of nonce, ciphertext and tag become 77 base32 symbols, plus the leading
/// key-version character. There is no "typical" and "extreme" case — the payload is
/// fixed-width, so the length never varies. That is why <c>GateScans.QrIdAtScan</c>
/// is nvarchar(96): 78 fits with room for the ceiling check to be a real guard
/// rather than a coincidence.</para>
/// </summary>
public static class EventBadgeCodec
{
    /// <summary>AES-GCM nonce length. Fixed at 12 by <see cref="AesGcm.NonceByteSizes"/>.</summary>
    public const int NonceBytes = 12;

    /// <summary>
    /// AES-GCM tag length. The FULL 16 bytes.
    ///
    /// <para>This was corrected from the 12-byte .NET minimum, which was
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

    /// <summary>The plaintext width: a 16-byte profile id, a 2-byte edition year
    /// and a 2-byte profile-type code.</summary>
    public const int PlaintextBytes = 20;

    /// <summary>Upper bound on an accepted scan, so a hostile or garbled input
    /// cannot push work into the decoder. A real badge is 78 characters.</summary>
    public const int MaxEncodedLength = 128;

    /// <summary>
    /// Builds the badge string. <paramref name="keyVersion"/> is emitted as a
    /// single leading Crockford character, so versions 0..31 are available.
    /// </summary>
    public static string Encode(
        EventBadgePayload payload, ReadOnlySpan<byte> key, int keyVersion)
    {
        if (payload.ProfileTypeCode is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload), "The profile-type code must fit in two bytes.");
        }
        if (payload.EditionYear is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload), "The edition year must fit in two bytes.");
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

        var plaintext = new byte[PlaintextBytes];
        // A fixed 16-byte layout, not the .NET Guid byte order, so the Dart
        // scanner reads the same id back without knowing about mixed-endian
        // Guid internals.
        WriteGuidBigEndian(payload.ProfileId, plaintext.AsSpan(0, 16));
        BinaryPrimitives.WriteUInt16BigEndian(
            plaintext.AsSpan(16, 2), (ushort)payload.EditionYear);
        BinaryPrimitives.WriteUInt16BigEndian(
            plaintext.AsSpan(18, 2), (ushort)payload.ProfileTypeCode);

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

        // A fixed-width payload, so anything else is a successful decrypt of
        // something this system did not author. There is no ASCII check any
        // more: every byte is legitimately arbitrary now.
        if (plaintext.Length != PlaintextBytes) { return false; }

        payload = new EventBadgePayload(
            ReadGuidBigEndian(plaintext[..16]),
            BinaryPrimitives.ReadUInt16BigEndian(plaintext.Slice(16, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(plaintext.Slice(18, 2)));
        return true;
    }

    /// <summary>Writes a Guid in a fixed byte order rather than .NET's, whose
    /// first three fields are little-endian on this platform and would decode to
    /// a different id on the scanner.</summary>
    private static void WriteGuidBigEndian(Guid value, Span<byte> destination)
    {
        value.TryWriteBytes(destination, bigEndian: true, out _);
    }

    private static Guid ReadGuidBigEndian(ReadOnlySpan<byte> source) =>
        new(source, bigEndian: true);
}
