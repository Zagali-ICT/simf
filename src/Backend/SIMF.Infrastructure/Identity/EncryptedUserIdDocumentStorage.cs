// Tests: SIMF.Api.Tests/UserProfileTests.cs (encrypt-then-decrypt round-trip,
//        missing-key startup gate)
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// AES-GCM encrypted-at-rest storage for the user's ID-document image
/// (decisions D-046 b, P8 — D-049; renamed from
/// <c>EncryptedVisitorIdStorage</c>).
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
/// <para>Key source: configuration <c>Storage:UserIdDocumentEncryptionKey</c>
/// — a base64-encoded 32-byte AES key. The constructor refuses to start
/// without it, the same startup-gate pattern the JWT signing key uses
/// (SIMF-FDS-001 Amendment A.2). Rotate by changing the key + re-encrypting
/// every file (operational task; out of scope here).</para>
/// </summary>
internal sealed class EncryptedUserIdDocumentStorage : IUserIdDocumentStorage
{
    private const int KeyLengthBytes = 32;        // AES-256
    private const int NonceLengthBytes = 12;      // AES-GCM standard
    private const int TagLengthBytes = 16;        // AES-GCM standard
    private const int ContentTypePrefixBytes = 1;
    private const int HeaderBytes = ContentTypePrefixBytes + NonceLengthBytes + TagLengthBytes;

    private readonly string _baseDirectory;
    private readonly byte[] _key;
    private readonly ILogger<EncryptedUserIdDocumentStorage> _logger;

    public EncryptedUserIdDocumentStorage(
        IOptions<StorageOptions> options,
        ILogger<EncryptedUserIdDocumentStorage> logger)
    {
        // R1 — D-074: typed options replace the raw IConfiguration[…] reads.
        // The required-key checks stay here (rather than ValidateOnStart) so
        // the failure modes and messages stay identical to the pre-R1 shape.
        var settings = options.Value;
        var configuredDirectory = settings.UserIdDocumentBase;
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:UserIdDocumentBase' is required but was not found.");
        }
        _baseDirectory = Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(_baseDirectory);

        var configuredKey = settings.UserIdDocumentEncryptionKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:UserIdDocumentEncryptionKey' is required "
                + "but was not found. Provide a base64-encoded 32-byte AES key.");
        }
        try
        {
            _key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "'Storage:UserIdDocumentEncryptionKey' must be a base64-encoded value.", ex);
        }
        if (_key.Length != KeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"'Storage:UserIdDocumentEncryptionKey' must decode to exactly {KeyLengthBytes} bytes; "
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
            "Encrypted user ID-document saved at {Path} ({CipherBytes} bytes, {ContentType})",
            relativePath, file.Length, contentType);
        return relativePath;
    }

    public Task<UserIdDocumentRead?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<UserIdDocumentRead?>(null);
        }

        var file = File.ReadAllBytes(fullPath);
        if (file.Length < HeaderBytes)
        {
            _logger.LogWarning(
                "User ID-document file {Path} is shorter than the header — refusing to decrypt.",
                relativePath);
            return Task.FromResult<UserIdDocumentRead?>(null);
        }

        var contentType = ContentTypeForCode(file[0]);
        if (contentType is null)
        {
            _logger.LogWarning(
                "User ID-document file {Path} has an unknown content-type code {Code}.",
                relativePath, file[0]);
            return Task.FromResult<UserIdDocumentRead?>(null);
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
                "Decryption of user ID-document {Path} failed — tag mismatch or wrong key.",
                relativePath);
            return Task.FromResult<UserIdDocumentRead?>(null);
        }
        return Task.FromResult<UserIdDocumentRead?>(new UserIdDocumentRead(plaintext, contentType));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Encrypted user ID-document deleted at {Path}", relativePath);
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
                "Rejected user ID-document path {Path} — outside the storage base directory.",
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
