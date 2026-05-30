using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Media;

/// <summary>D-199 — filesystem-backed media image storage. Mirrors
/// <c>FilesystemAvatarStorage</c>: writes under a configured root and returns
/// a relative path the caller persists; the bytes never touch the database
/// row (D-90). One file per (item, slot); re-upload replaces in place.</summary>
public sealed class FilesystemMediaImageStorage(
    IOptions<MediaImageStorageOptions> options) : IMediaImageStorage
{
    private readonly string _root = options.Value.RootPath;

    public async Task<string> SaveAsync(
        Guid mediaItemId, MediaImageSlot slot, byte[] content, string contentType,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin",
        };
        var suffix = slot == MediaImageSlot.Thumbnail ? "thumb" : "image";
        var fileName = $"{mediaItemId:N}-{suffix}{ext}";
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
