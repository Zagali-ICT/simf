// Tests: SIMF.Api.Tests/Files/FilesystemFileStorageProviderTests.cs
//        (round-trip, sibling-prefix path guard, encrypt-at-rest, secure-erase).
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Files;

/// <summary>
/// D-568 — filesystem-backed <see cref="IFileStorageProvider"/>. One root with a
/// per-<see cref="FileService"/> sub-folder; the file name is always
/// <c>{id:N}{ext}</c> built from our own GUID + a sanitized extension, never from
/// client input, so path traversal is impossible by construction. Writes are
/// atomic (temp + move) so a partial write never replaces a good file; encrypted
/// services seal the bytes through <see cref="IFileCipher"/> before they touch the
/// disk. The read path re-validates the stored key against the root (defence in
/// depth), with the separator-boundary fix that closes the sibling-prefix bypass
/// the legacy guards had.
/// </summary>
internal sealed class FilesystemFileStorageProvider : IFileStorageProvider
{
    // Crypto-shred range: overwriting the first bytes destroys the envelope header
    // (fmt + KEK version + wrapped DEK), rendering an encrypted body unrecoverable.
    private const int ShredHeaderBytes = 4096;

    // Copy buffer for the streamed (recording) read/write paths (D-568 S7).
    private const int StreamBufferSize = 81920;

    private readonly string _root;
    private readonly IFileCipher _cipher;
    private readonly ILogger<FilesystemFileStorageProvider> _logger;

    public FilesystemFileStorageProvider(
        IOptions<FileStorageOptions> options,
        IFileCipher cipher,
        ILogger<FilesystemFileStorageProvider> logger)
    {
        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? DefaultRoot() : configured);
        Directory.CreateDirectory(_root);
        _cipher = cipher;
        _logger = logger;
    }

    /// <summary>Where the store lives when <c>FileStorage:RootPath</c> is unset:
    /// a SIMF folder under the machine's shared application-data directory
    /// (<c>C:\ProgramData\SIMF\files</c> on Windows). Absolute on purpose, so an
    /// unconfigured run writes to the machine's data store rather than to
    /// whatever directory the process happens to have started in.</summary>
    internal static string DefaultRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SIMF",
            "files");

    public async Task<FileWriteResult> WriteAsync(
        FileService service, Guid fileId, string extension, byte[] content, bool encrypt,
        CancellationToken cancellationToken = default)
    {
        var storageKey = BuildStorageKey(service, fileId, extension);
        var fullPath = ResolveSafe(storageKey)
            ?? throw new InvalidOperationException($"Server-built storage key '{storageKey}' failed validation.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var bytes = encrypt ? _cipher.Encrypt(content) : content;

        var temp = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        File.Move(temp, fullPath, overwrite: true);

        _logger.LogInformation(
            "Stored file {Key} ({Bytes} bytes on disk, encrypted={Encrypted}).",
            storageKey, bytes.Length, encrypt);

        return new FileWriteResult(storageKey, encrypt ? _cipher.CurrentFormatVersion : (byte)0);
    }

    public async Task<byte[]?> ReadAsync(
        string storageKey, bool encrypted, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (fullPath is null || !File.Exists(fullPath)) { return null; }

        var raw = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return encrypted ? _cipher.Decrypt(raw) : raw;
    }

    public async Task<StreamWriteResult> WriteStreamAsync(
        FileService service, Guid fileId, string extension, Stream content,
        CancellationToken cancellationToken = default)
    {
        var storageKey = BuildStorageKey(service, fileId, extension);
        var fullPath = ResolveSafe(storageKey)
            ?? throw new InvalidOperationException($"Server-built storage key '{storageKey}' failed validation.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // Stream source → temp with an incremental SHA-256, then atomic-move (temp +
        // move) so a partial write never replaces a good file. Plaintext only — the
        // file must stay seekable for Range streaming (D-568 S7).
        var temp = fullPath + ".tmp";
        long total = 0;
        string sha256Hex;
        using (var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256))
        {
            await using (var dest = new FileStream(
                temp, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferSize, FileOptions.Asynchronous))
            {
                var buffer = new byte[StreamBufferSize];
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, StreamBufferSize), cancellationToken)) > 0)
                {
                    hash.AppendData(buffer.AsSpan(0, read));
                    await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                }
            }
            sha256Hex = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        File.Move(temp, fullPath, overwrite: true);

        _logger.LogInformation(
            "Streamed file {Key} ({Bytes} bytes on disk, plaintext).", storageKey, total);
        return new StreamWriteResult(storageKey, sha256Hex, total);
    }

    public Task<FileReadStream?> OpenReadAsync(
        string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (fullPath is null || !File.Exists(fullPath)) { return Task.FromResult<FileReadStream?>(null); }
        var info = new FileInfo(fullPath);
        var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, FileOptions.Asynchronous);
        return Task.FromResult<FileReadStream?>(new FileReadStream(stream, info.Length));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (fullPath is not null && File.Exists(fullPath)) { File.Delete(fullPath); }
        return Task.CompletedTask;
    }

    public async Task SecureEraseAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (fullPath is null || !File.Exists(fullPath)) { return; }

        // Overwrite the header region (which holds the wrapped DEK for an encrypted
        // file) with random bytes, flush, then unlink. For an encrypted file this is
        // a true crypto-shred; for a plaintext file it scrambles the head before
        // unlinking.
        var length = new FileInfo(fullPath).Length;
        var overwrite = (int)Math.Min(length, ShredHeaderBytes);
        if (overwrite > 0)
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(overwrite);
            await using var stream = new FileStream(
                fullPath, FileMode.Open, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(random, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Delete(fullPath);
        _logger.LogInformation("Securely erased file {Key}.", storageKey);
    }

    private string BuildStorageKey(FileService service, Guid fileId, string extension) =>
        $"{service.ToString().ToLowerInvariant()}/{fileId:N}{NormalizeExtension(extension)}";

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) { return ".bin"; }
        var ext = extension.Trim();
        if (!ext.StartsWith('.')) { ext = "." + ext; }
        // Only a dot + ASCII alphanumerics, max 16 chars; anything else falls back.
        if (ext.Length > 16) { return ".bin"; }
        for (var i = 1; i < ext.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(ext[i])) { return ".bin"; }
        }
        return ext.ToLowerInvariant();
    }

    /// <summary>Resolves a storage key to an absolute path and confirms it stays
    /// within the root. Compares against <c>root + separator</c> (not a bare
    /// prefix), so a sibling directory whose name starts with the root cannot be
    /// reached. Returns null on any escape.</summary>
    private string? ResolveSafe(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) { return null; }
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected storage key {Key} — outside the file-storage root.", storageKey);
            return null;
        }
        return full;
    }
}
