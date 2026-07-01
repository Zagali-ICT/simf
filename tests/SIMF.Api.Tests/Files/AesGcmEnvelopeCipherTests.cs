using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SIMF.Common.Options;
using SIMF.Infrastructure.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>D-568 — unit cover for the envelope cipher: round-trip, tamper-detect
/// (body + wrapped-DEK), the boot-fail-fast key gate, and the rotation window where
/// a previous KEK still decrypts older blobs. No host, DB or filesystem.</summary>
public sealed class AesGcmEnvelopeCipherTests
{
    private const int HeaderLen = 90; // 1+1 + 12+16+32 + 12+16

    // 32 distinct bytes per seed → a valid, distinct AES-256 key.
    private static string Key(byte seed) =>
        Convert.ToBase64String(Enumerable.Range(seed, 32).Select(i => (byte)i).ToArray());

    private static AesGcmEnvelopeCipher Cipher(
        string activeKey, byte activeVersion = 1,
        string? previousKey = null, byte previousVersion = 0) =>
        new(Options.Create(new FileStorageOptions
        {
            EncryptionKey = activeKey,
            KekVersion = activeVersion,
            PreviousEncryptionKey = previousKey ?? string.Empty,
            PreviousKekVersion = previousVersion,
        }));

    [Fact]
    public void Round_trip_returns_the_original_plaintext()
    {
        var cipher = Cipher(Key(0));
        var plaintext = "the quick brown fox — مرحبا"u8.ToArray();

        var blob = cipher.Encrypt(plaintext);
        var recovered = cipher.Decrypt(blob);

        Assert.Equal(plaintext, recovered);
        Assert.Equal((byte)1, cipher.CurrentFormatVersion);
    }

    [Fact]
    public void Ciphertext_is_longer_than_the_plaintext_by_exactly_the_header()
    {
        var cipher = Cipher(Key(0));
        var plaintext = new byte[1000];

        var blob = cipher.Encrypt(plaintext);

        Assert.Equal(plaintext.Length + HeaderLen, blob.Length);
        Assert.NotEqual(plaintext, blob[HeaderLen..]); // body is actually enciphered
    }

    [Fact]
    public void Empty_plaintext_round_trips()
    {
        var cipher = Cipher(Key(3));

        var blob = cipher.Encrypt([]);

        Assert.Equal(HeaderLen, blob.Length);
        Assert.Empty(cipher.Decrypt(blob));
    }

    [Fact]
    public void Tampering_with_the_body_is_detected()
    {
        var cipher = Cipher(Key(0));
        var blob = cipher.Encrypt("secret-body"u8.ToArray());

        blob[^1] ^= 0xFF; // flip a ciphertext byte

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(blob));
    }

    [Fact]
    public void Tampering_with_the_wrapped_dek_is_detected()
    {
        var cipher = Cipher(Key(0));
        var blob = cipher.Encrypt("secret-body"u8.ToArray());

        blob[3] ^= 0xFF; // flip a byte inside the DEK-nonce/tag/wrapped-DEK header region

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(blob));
    }

    [Fact]
    public void A_blob_shorter_than_the_header_is_rejected()
    {
        var cipher = Cipher(Key(0));

        Assert.Throws<NotSupportedException>(() => cipher.Decrypt(new byte[HeaderLen - 1]));
    }

    [Fact]
    public void An_unsupported_format_version_is_rejected()
    {
        var cipher = Cipher(Key(0));
        var blob = cipher.Encrypt("x"u8.ToArray());

        blob[0] = 99; // format-version byte

        Assert.Throws<NotSupportedException>(() => cipher.Decrypt(blob));
    }

    [Fact]
    public void A_blob_wrapped_under_an_unknown_kek_version_is_rejected()
    {
        var blob = Cipher(Key(0), activeVersion: 1).Encrypt("x"u8.ToArray());

        // A cipher that only knows KEK version 2 cannot resolve the v1 blob.
        var other = Cipher(Key(5), activeVersion: 2);

        Assert.Throws<NotSupportedException>(() => other.Decrypt(blob));
    }

    [Fact]
    public void A_missing_key_is_a_boot_failure()
    {
        Assert.Throws<InvalidOperationException>(() => Cipher(string.Empty));
    }

    [Fact]
    public void A_non_base64_key_is_a_boot_failure()
    {
        Assert.Throws<InvalidOperationException>(() => Cipher("not-base64-!!!"));
    }

    [Fact]
    public void A_wrong_length_key_is_a_boot_failure()
    {
        var sixteenBytes = Convert.ToBase64String(new byte[16]);

        Assert.Throws<InvalidOperationException>(() => Cipher(sixteenBytes));
    }

    [Fact]
    public void A_previous_kek_still_decrypts_a_blob_written_before_rotation()
    {
        // Old blob written under KEK v1.
        var blobV1 = Cipher(Key(1), activeVersion: 1).Encrypt("pre-rotation"u8.ToArray());

        // After rotation the active KEK is v2, but v1 is kept in the rotation window.
        var rotated = Cipher(Key(2), activeVersion: 2, previousKey: Key(1), previousVersion: 1);

        Assert.Equal("pre-rotation"u8.ToArray(), rotated.Decrypt(blobV1));

        // And a freshly written blob (now under v2) still round-trips.
        var blobV2 = rotated.Encrypt("post-rotation"u8.ToArray());
        Assert.Equal("post-rotation"u8.ToArray(), rotated.Decrypt(blobV2));
    }
}
