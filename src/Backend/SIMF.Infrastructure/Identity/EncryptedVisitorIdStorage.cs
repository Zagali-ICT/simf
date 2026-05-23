// Tests: SIMF.Api.Tests/VisitorProfileTests.cs (encrypt-then-decrypt round-trip,
//        missing-key startup gate)
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIMF.Application.IdentityAccess.Abstractions;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// AES-GCM encrypted-at-rest storage for visitor ID images (decision D-046 b).
///
/// <para>File format on disk (single binary blob):</para>
/// <code>
///   [1 byte content-type code][12-byte nonce][16-byte GCM tag][N bytes ciphertext]
/// </code>
///
/// <para>Content-type codes:</para>
/// <code>
///   1 = image/jpeg, 2 = image/png, 3 = image/webp
/// </code>
///
/// <para>Key source: configuration <c>Storage:VisitorIdEncryptionKey</c> —
/// a base64-encoded 32-byte AES key. The constructor refuses to start
/// without it, the same startup-gate pattern the JWT signing key uses
/// (SIMF-FDS-001 Amendment A.2). Rotate by changing the key + re-encrypting
/// every file (operational task; out of scope here).</para>
/// </summary>
internal sealed class EncryptedVisitorIdStorage : IVisitorIdStorage
{
    private const int KeyLengthBytes = 32;        // AES-256
    private const int NonceLengthBytes = 12;      // AES-GCM standard
    private const int TagLengthBytes = 16;        // AES-GCM standard
    private const int ContentTypePrefixBytes = 1;
    private const int HeaderBytes = ContentTypePrefixBytes + NonceLengthBytes + TagLengthBytes;

    private readonly string _baseDirectory;
    private readonly byte[] _key;
    private readonly ILogger<EncryptedVisitorIdStorage> _logger;

    public EncryptedVisitorIdStorage(
        IConfiguration configuration,
        ILogger<EncryptedVisitorIdStorage> logger)
    {
        var configuredDirectory = configuration["Storage:VisitorIdBase"];
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:VisitorIdBase' is required but was not found.");
        }
        _baseDirectory = Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(_baseDirectory);

        var configuredKey = configuration["Storage:VisitorIdEncryptionKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:VisitorIdEncryptionKey' is required "
                + "but was not found. Provide a base64-encoded 32-byte AES key.");
        }
        try
        {
            _key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "'Storage:VisitorIdEncryptionKey' must be a base64-encoded value.", ex);
        }
        if (_key.Length != KeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"'Storage:VisitorIdEncryptionKey' must decode to exactly {KeyLengthBytes} bytes; "
                + $"got {_key.Length}.");
        }

        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Guid userId, byte[] content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var contentTypeCode = ContentTypeCode(contentType);
        var nonce = new byte[NonceLengthBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[content.Length];
        var tag = new byte[TagLengthBytes];
        using (var aes = new AesGcm(_key, TagLengthBytes))
        {
            aes.Encrypt(nonce, content, ciphertext, tag);
        }

        var file = new byte[HeaderBytes + ciphertext.Length];
        file[0] = contentTypeCode;
        Buffer.BlockCopy(nonce, 0, file, ContentTypePrefixBytes, NonceLengthBytes);
        Buffer.BlockCopy(tag, 0, file, ContentTypePrefixBytes + NonceLengthBytes, TagLengthBytes);
        Buffer.BlockCopy(ciphertext, 0, file, HeaderBytes, ciphertext.Length);

        // Filename is server-controlled (userId hex + .bin) — no path
        // traversal possible. Atomic write via temp + move so a partial
        // write never replaces a previous file.
        var relativePath = $"{userId:N}.bin";
        var fullPath = Path.Combine(_baseDirectory, relativePath);
        var temp = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temp, file, cancellationToken);
        File.Move(temp, fullPath, overwrite: true);

        _logger.LogInformation(
            "Encrypted visitor ID-image saved at {Path} ({CipherBytes} bytes, {ContentType})",
            relativePath, file.Length, contentType);
        return relativePath;
    }

    public Task<VisitorIdRead?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<VisitorIdRead?>(null);
        }

        var file = File.ReadAllBytes(fullPath);
        if (file.Length < HeaderBytes)
        {
            _logger.LogWarning(
                "Visitor ID-image file {Path} is shorter than the header — refusing to decrypt.",
                relativePath);
            return Task.FromResult<VisitorIdRead?>(null);
        }

        var contentType = ContentTypeForCode(file[0]);
        if (contentType is null)
        {
            _logger.LogWarning(
                "Visitor ID-image file {Path} has an unknown content-type code {Code}.",
                relativePath, file[0]);
            return Task.FromResult<VisitorIdRead?>(null);
        }

        var nonce = new byte[NonceLengthBytes];
        var tag = new byte[TagLengthBytes];
        var ciphertext = new byte[file.Length - HeaderBytes];
        Buffer.BlockCopy(file, ContentTypePrefixBytes, nonce, 0, NonceLengthBytes);
        Buffer.BlockCopy(file, ContentTypePrefixBytes + NonceLengthBytes, tag, 0, TagLengthBytes);
        Buffer.BlockCopy(file, HeaderBytes, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(_key, TagLengthBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex,
                "Decryption of visitor ID-image {Path} failed — tag mismatch or wrong key.",
                relativePath);
            return Task.FromResult<VisitorIdRead?>(null);
        }
        return Task.FromResult<VisitorIdRead?>(new VisitorIdRead(plaintext, contentType));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Encrypted visitor ID-image deleted at {Path}", relativePath);
        }
        return Task.CompletedTask;
    }

    private string? ResolveSafe(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
        if (!full.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Rejected visitor ID-image path {Path} — outside the storage base directory.",
                relativePath);
            return null;
        }
        return full;
    }

    private static byte ContentTypeCode(string contentType) => contentType switch
    {
        "image/jpeg" => 1,
        "image/png" => 2,
        "image/webp" => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(contentType),
            contentType, "Unsupported content type — the caller's magic-byte gate should have rejected this."),
    };

    private static string? ContentTypeForCode(byte code) => code switch
    {
        1 => "image/jpeg",
        2 => "image/png",
        3 => "image/webp",
        _ => null,
    };
}
