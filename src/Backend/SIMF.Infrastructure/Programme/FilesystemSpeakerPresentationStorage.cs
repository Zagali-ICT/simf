using Microsoft.Extensions.Options;
using SIMF.Application.Programme.Abstractions;

namespace SIMF.Infrastructure.Programme;

/// <summary>P2.3 — D-228: filesystem-backed speaker-presentation storage.
/// Mirrors <c>FilesystemMediaImageStorage</c>: writes under a configured root
/// and returns the stored file name the caller persists; the bytes never touch
/// the database row (D-90). The stored name keeps the original extension so a
/// download serves a sensible file; the content type is carried on the row.</summary>
public sealed class FilesystemSpeakerPresentationStorage(
    IOptions<SpeakerPresentationStorageOptions> options) : ISpeakerPresentationStorage
{
    private readonly string _root = options.Value.RootPath;

    public async Task<string> SaveAsync(
        Guid presentationId, byte[] content, string originalFileName,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var ext = Path.GetExtension(originalFileName);
        if (ext.Length is 0 or > 16)
        {
            ext = ".bin";
        }
        var fileName = $"{presentationId:N}{ext}";
        var fullPath = Path.Combine(_root, fileName);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fileName;
    }

    public async Task<(byte[] Content, string ContentType)?> GetAsync(
        string storedFileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, storedFileName);
        if (!File.Exists(fullPath)) { return null; }
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var resolved = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;
        return (bytes, resolved);
    }

    public Task DeleteAsync(
        string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, storedFileName);
        if (File.Exists(fullPath)) { File.Delete(fullPath); }
        return Task.CompletedTask;
    }
}
