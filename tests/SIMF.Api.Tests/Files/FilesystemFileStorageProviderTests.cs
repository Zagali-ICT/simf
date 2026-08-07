using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Infrastructure.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>D-568 — unit cover for the filesystem provider: plaintext round-trip,
/// encrypt-at-rest (ciphertext on disk, plaintext on read), the separator-boundary
/// path guard that closes the sibling-prefix bypass, and secure-erase. Each test
/// uses its own throwaway root, cleaned up on Dispose.</summary>
public sealed class FilesystemFileStorageProviderTests : IDisposable
{
    // A real 32-byte test KEK so the encrypt-at-rest path exercises the cipher.
    private const string TestKey = "U0lNRnRlc3RGaWxlU3RvcmFnZUtFSzAxMjM0NTY3ODk=";

    private readonly List<string> _tempDirs = [];

    private FilesystemFileStorageProvider NewProvider(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), $"simf-fs-{Guid.NewGuid():N}");
        _tempDirs.Add(root);
        var cipher = new AesGcmEnvelopeCipher(
            Options.Create(new FileStorageOptions { EncryptionKey = TestKey }));
        return new FilesystemFileStorageProvider(
            Options.Create(new FileStorageOptions { RootPath = root, EncryptionKey = TestKey }),
            cipher,
            NullLogger<FilesystemFileStorageProvider>.Instance);
    }

    private static string DiskPath(string root, string storageKey) =>
        Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public async Task Write_then_read_round_trips_a_plaintext_file()
    {
        var provider = NewProvider(out var root);
        var id = Guid.NewGuid();
        var bytes = "hello plaintext"u8.ToArray();

        var write = await provider.WriteAsync(FileService.SpeakerPhoto, id, ".png", bytes, encrypt: false);

        Assert.Equal($"speakerphoto/{id:N}.png", write.StorageKey);
        Assert.Equal((byte)0, write.CipherFormatVersion); // no cipher used
        Assert.Equal(bytes, await provider.ReadAsync(write.StorageKey, encrypted: false));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(DiskPath(root, write.StorageKey))); // bytes are raw on disk
    }

    [Fact]
    public async Task Encrypt_at_rest_stores_ciphertext_but_reads_back_plaintext()
    {
        var provider = NewProvider(out var root);
        var id = Guid.NewGuid();
        var bytes = "confidential body"u8.ToArray();

        var write = await provider.WriteAsync(FileService.Avatar, id, ".png", bytes, encrypt: true);

        Assert.Equal((byte)1, write.CipherFormatVersion);

        var onDisk = await File.ReadAllBytesAsync(DiskPath(root, write.StorageKey));
        Assert.NotEqual(bytes, onDisk);                 // not the plaintext
        Assert.Equal(bytes.Length + 90, onDisk.Length); // header-framed envelope blob

        Assert.Equal(bytes, await provider.ReadAsync(write.StorageKey, encrypted: true));
    }

    [Fact]
    public async Task Reading_a_missing_key_returns_null()
    {
        var provider = NewProvider(out _);

        Assert.Null(await provider.ReadAsync($"avatar/{Guid.NewGuid():N}.png", encrypted: false));
    }

    [Fact]
    public async Task A_traversal_key_is_rejected_and_reads_null()
    {
        var provider = NewProvider(out _);

        Assert.Null(await provider.ReadAsync("../escape.bin", encrypted: false));
        Assert.Null(await provider.ReadAsync("..\\..\\escape.bin", encrypted: false));
    }

    [Fact]
    public async Task A_sibling_directory_sharing_the_root_prefix_cannot_be_reached()
    {
        var provider = NewProvider(out var root);

        // A sibling whose name STARTS WITH the root would pass a naive prefix check
        // but is outside the store. Plant a real file there, then prove the guard
        // (root + separator) refuses to read it.
        var sibling = root + "evil";
        _tempDirs.Add(sibling);
        Directory.CreateDirectory(sibling);
        var plantedPath = Path.Combine(sibling, "secret.bin");
        await File.WriteAllBytesAsync(plantedPath, "planted"u8.ToArray());

        var traversalKey = $"../{Path.GetFileName(root)}evil/secret.bin";

        Assert.Null(await provider.ReadAsync(traversalKey, encrypted: false));
        Assert.True(File.Exists(plantedPath)); // and the planted file is left untouched
    }

    [Fact]
    public async Task Exists_reports_presence_and_never_escapes_the_root()
    {
        // ExistsAsync is what lets a caller tell "this pointer resolves to bytes"
        // from "this pointer is a corpse" without reading (and decrypting) the file.
        // It answers off the SAME guarded path resolution as ReadAsync, so it can
        // never become an out-of-store existence oracle: a traversal key reports
        // false even when a real file sits at the target.
        var provider = NewProvider(out var root);
        var write = await provider.WriteAsync(
            FileService.Avatar, Guid.NewGuid(), ".png", "here"u8.ToArray(), encrypt: true);

        Assert.True(await provider.ExistsAsync(write.StorageKey));
        Assert.False(await provider.ExistsAsync($"avatar/{Guid.NewGuid():N}.png"));
        Assert.False(await provider.ExistsAsync("../escape.bin"));

        var sibling = root + "evil";
        _tempDirs.Add(sibling);
        Directory.CreateDirectory(sibling);
        await File.WriteAllBytesAsync(Path.Combine(sibling, "secret.bin"), "planted"u8.ToArray());
        Assert.False(
            await provider.ExistsAsync($"../{Path.GetFileName(root)}evil/secret.bin"),
            "a key outside the store must report absent, whatever is really there");

        // And once the bytes go, it flips — the dangling-pointer case.
        await provider.DeleteAsync(write.StorageKey);
        Assert.False(await provider.ExistsAsync(write.StorageKey));
    }

    [Fact]
    public async Task Secure_erase_removes_the_file()
    {
        var provider = NewProvider(out var root);
        var id = Guid.NewGuid();
        var write = await provider.WriteAsync(
            FileService.IdDocument, id, ".pdf", "shred me"u8.ToArray(), encrypt: true);
        var path = DiskPath(root, write.StorageKey);
        Assert.True(File.Exists(path));

        await provider.SecureEraseAsync(write.StorageKey);

        Assert.False(File.Exists(path));
        Assert.Null(await provider.ReadAsync(write.StorageKey, encrypted: true));
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
            }
            catch
            {
                // Best-effort cleanup of throwaway test directories.
            }
        }
    }
}
