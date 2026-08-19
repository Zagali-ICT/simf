using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// A2-10 — AES-256-GCM encryptor for PII identifier columns (see
/// <see cref="IPiiEncryptor"/>). Reuses the existing
/// <c>Storage:UserIdDocumentEncryptionKey</c> (a base64 32-byte key) so no new
/// secret has to be provisioned; the ID-document image already protects the same
/// sensitivity class with the same key.
///
/// <para>The constructor is deliberately tolerant of a missing/invalid key (it
/// leaves the key unset) so EF design-time tooling — which builds the model but
/// never encrypts/decrypts — works even when the dev config has the key blanked.
/// At runtime the Production boot guard
/// (<c>DependencyInjection.EnsurePiiEncryptionConfigured</c>) refuses to start
/// without a valid key, and any actual encrypt/decrypt throws if the key is
/// absent.</para>
/// </summary>
internal sealed class AesGcmPiiEncryptor : IPiiEncryptor
{
    private const string Marker = "enc:1:";
    private const int KeyLengthBytes = 32;   // AES-256
    private const int NonceLengthBytes = 12; // AES-GCM standard
    private const int TagLengthBytes = 16;   // AES-GCM standard

    private readonly byte[]? _key;

    public AesGcmPiiEncryptor(IOptions<StorageOptions> options)
    {
        var configured = options.Value.UserIdDocumentEncryptionKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return; // _key stays null — tolerated at design time, gated at boot.
        }
        try
        {
            var bytes = Convert.FromBase64String(configured);
            if (bytes.Length == KeyLengthBytes)
            {
                _key = bytes;
            }
        }
        catch (FormatException)
        {
            // Not valid base64 — leave the key unset; the boot guard reports it.
        }
    }

    public bool IsKeyConfigured => _key is not null;

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }
        if (plaintext.StartsWith(Marker, StringComparison.Ordinal))
        {
            return plaintext; // already encrypted — idempotent.
        }
        return Seal(plaintext);
    }

    public string? EncryptFreeText(string? plaintext) =>
        string.IsNullOrEmpty(plaintext) ? plaintext : Seal(plaintext);

    private string Seal(string plaintext)
    {
        var key = RequireKey();

        var nonce = new byte[NonceLengthBytes];
        RandomNumberGenerator.Fill(nonce);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagLengthBytes];
        using (var aes = new AesGcm(key, TagLengthBytes))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var blob = new byte[NonceLengthBytes + TagLengthBytes + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceLengthBytes);
        Buffer.BlockCopy(tag, 0, blob, NonceLengthBytes, TagLengthBytes);
        Buffer.BlockCopy(cipher, 0, blob, NonceLengthBytes + TagLengthBytes, cipher.Length);
        return Marker + Convert.ToBase64String(blob);
    }

    public string? Decrypt(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Marker, StringComparison.Ordinal))
        {
            return stored; // null/empty or legacy plaintext — return unchanged.
        }
        var key = RequireKey();

        var blob = Convert.FromBase64String(stored[Marker.Length..]);
        if (blob.Length < NonceLengthBytes + TagLengthBytes)
        {
            throw new CryptographicException("Encrypted PII value is shorter than the header.");
        }
        var nonce = new byte[NonceLengthBytes];
        var tag = new byte[TagLengthBytes];
        var cipher = new byte[blob.Length - NonceLengthBytes - TagLengthBytes];
        Buffer.BlockCopy(blob, 0, nonce, 0, NonceLengthBytes);
        Buffer.BlockCopy(blob, NonceLengthBytes, tag, 0, TagLengthBytes);
        Buffer.BlockCopy(blob, NonceLengthBytes + TagLengthBytes, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using var aesDecrypt = new AesGcm(key, TagLengthBytes);
        aesDecrypt.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string? BlindIndex(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var key = RequireKey();
        // Deterministic (unlike Encrypt): the SAME plaintext always yields the SAME
        // digest, so the H-1 duplicate-identity guard + its filtered UNIQUE indexes
        // work — the encrypted columns cannot, because Encrypt uses a random nonce.
        // Keyed HMAC (not a bare hash) so a DB leak cannot brute-force the small
        // identifier space back to plaintext offline.
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(digest); // 64 hex chars
    }

    private byte[] RequireKey() =>
        _key ?? throw new InvalidOperationException(
            "PII encryption key is not configured (Storage:UserIdDocumentEncryptionKey).");
}
