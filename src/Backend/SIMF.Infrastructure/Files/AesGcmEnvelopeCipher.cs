// Tests: SIMF.Api.Tests/Files/AesGcmEnvelopeCipherTests.cs (round-trip,
//        tamper-detect, missing-key gate, crypto-shred-by-header).
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Files;

/// <summary>
/// AES-256-GCM envelope cipher for stored-file bytes.
///
/// <para>On-disk blob (<c>CurrentFormatVersion = 1</c>):</para>
/// <code>
///   [1 fmt-version][1 kek-version]
///   [12 dek-nonce][16 dek-tag][32 wrapped-DEK]      ← the DEK sealed under the KEK
///   [12 content-nonce][16 content-tag][N ciphertext] ← the body sealed under the DEK
/// </code>
///
/// <para>Each file gets a fresh random 256-bit DEK and fresh 96-bit nonces, so a
/// nonce is used once per key — far below any GCM collision bound. The KEK comes
/// from <c>FileStorage:EncryptionKey</c> (base64 32-byte); the constructor refuses
/// to start without a valid one (the same boot-gate the JWT and ID-document keys
/// use). A previous KEK can be supplied during a rotation window so older blobs
/// still decrypt until they are re-wrapped.</para>
/// </summary>
internal sealed class AesGcmEnvelopeCipher : IFileCipher
{
    private const byte FormatVersion = 1;
    private const int KeyLen = 32;     // AES-256
    private const int NonceLen = 12;   // AES-GCM standard
    private const int TagLen = 16;     // AES-GCM standard
    private const int DekLen = 32;     // 256-bit per-file key

    // [fmt][kekVer][dekNonce][dekTag][wrappedDek][contentNonce][contentTag]
    private const int HeaderLen = 1 + 1 + NonceLen + TagLen + DekLen + NonceLen + TagLen;

    private readonly byte _activeKekVersion;
    private readonly IReadOnlyDictionary<byte, byte[]> _keks;

    public AesGcmEnvelopeCipher(IOptions<FileStorageOptions> options)
    {
        var settings = options.Value;
        var keks = new Dictionary<byte, byte[]>
        {
            [settings.KekVersion] = DecodeKey(settings.EncryptionKey, "FileStorage:EncryptionKey"),
        };
        if (!string.IsNullOrWhiteSpace(settings.PreviousEncryptionKey))
        {
            keks[settings.PreviousKekVersion] =
                DecodeKey(settings.PreviousEncryptionKey, "FileStorage:PreviousEncryptionKey");
        }
        _keks = keks;
        _activeKekVersion = settings.KekVersion;
    }

    public byte CurrentFormatVersion => FormatVersion;

    public byte ActiveKekVersion => _activeKekVersion;

    public byte[] Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var kek = _keks[_activeKekVersion];

        var dek = RandomNumberGenerator.GetBytes(DekLen);
        try
        {
            // Seal the body under the per-file DEK.
            var contentNonce = RandomNumberGenerator.GetBytes(NonceLen);
            var ciphertext = new byte[plaintext.Length];
            var contentTag = new byte[TagLen];
            using (var bodyCipher = new AesGcm(dek, TagLen))
            {
                bodyCipher.Encrypt(contentNonce, plaintext, ciphertext, contentTag);
            }

            // Seal the DEK under the KEK.
            var dekNonce = RandomNumberGenerator.GetBytes(NonceLen);
            var wrappedDek = new byte[DekLen];
            var dekTag = new byte[TagLen];
            using (var keyCipher = new AesGcm(kek, TagLen))
            {
                keyCipher.Encrypt(dekNonce, dek, wrappedDek, dekTag);
            }

            var blob = new byte[HeaderLen + ciphertext.Length];
            var offset = 0;
            blob[offset++] = FormatVersion;
            blob[offset++] = _activeKekVersion;
            offset = Append(blob, offset, dekNonce);
            offset = Append(blob, offset, dekTag);
            offset = Append(blob, offset, wrappedDek);
            offset = Append(blob, offset, contentNonce);
            offset = Append(blob, offset, contentTag);
            Append(blob, offset, ciphertext);
            return blob;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public byte[] Decrypt(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length < HeaderLen)
        {
            throw new NotSupportedException("Encrypted file blob is shorter than the header.");
        }

        var span = blob.AsSpan();
        var offset = 0;
        var format = span[offset++];
        if (format != FormatVersion)
        {
            throw new NotSupportedException($"Unsupported encrypted-file format version {format}.");
        }
        var kekVersion = span[offset++];
        if (!_keks.TryGetValue(kekVersion, out var kek))
        {
            throw new NotSupportedException(
                $"No KEK available for version {kekVersion}; a rotation key may be missing from configuration.");
        }

        var dekNonce = span.Slice(offset, NonceLen); offset += NonceLen;
        var dekTag = span.Slice(offset, TagLen); offset += TagLen;
        var wrappedDek = span.Slice(offset, DekLen); offset += DekLen;
        var contentNonce = span.Slice(offset, NonceLen); offset += NonceLen;
        var contentTag = span.Slice(offset, TagLen); offset += TagLen;
        var ciphertext = span[offset..];

        var dek = new byte[DekLen];
        try
        {
            using (var keyCipher = new AesGcm(kek, TagLen))
            {
                keyCipher.Decrypt(dekNonce, wrappedDek, dekTag, dek);
            }
            var plaintext = new byte[ciphertext.Length];
            using (var bodyCipher = new AesGcm(dek, TagLen))
            {
                bodyCipher.Decrypt(contentNonce, ciphertext, contentTag, plaintext);
            }
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    private static int Append(byte[] destination, int offset, ReadOnlySpan<byte> source)
    {
        source.CopyTo(destination.AsSpan(offset));
        return offset + source.Length;
    }

    private static byte[] DecodeKey(string base64, string configKey)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException(
                $"Configuration value '{configKey}' is required but was not found. "
                + "Provide a base64-encoded 32-byte AES key.");
        }
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"'{configKey}' must be a base64-encoded value.", ex);
        }
        if (key.Length != KeyLen)
        {
            throw new InvalidOperationException(
                $"'{configKey}' must decode to exactly {KeyLen} bytes; got {key.Length}.");
        }
        return key;
    }
}
