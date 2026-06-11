using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Assets;

/// <summary>D-357 — filesystem-backed storage for unified media assets. Mirrors
/// <c>FilesystemMediaImageStorage</c>: writes under a configured root and returns
/// a relative file name the caller persists; the bytes never touch the database
/// row (D-90). One file per asset id; re-upload replaces in place.</summary>
public sealed class FilesystemImageAssetStorage(
    IOptions<ImageAssetStorageOptions> options) : IImageAssetStorage
{
    private readonly string _root = options.Value.RootPath;

    public async Task<string> SaveAsync(
        Guid assetId, byte[] content, string contentType,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => ".bin",
        };
        var fileName = $"{assetId:N}{ext}";
        var fullPath = Path.Combine(_root, fileName);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fileName;
    }

    public async Task<(byte[] Content, string ContentType)?> GetAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, relativePath);
        if (!File.Exists(fullPath)) { return null; }
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var contentType = Path.GetExtension(fullPath) switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
        return (bytes, contentType);
    }

    public Task DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, relativePath);
        if (File.Exists(fullPath)) { File.Delete(fullPath); }
        return Task.CompletedTask;
    }
}
