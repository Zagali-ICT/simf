using Microsoft.Extensions.Options;
using SIMF.Application.Programme.Abstractions;

namespace SIMF.Infrastructure.Programme;

/// <summary>P3.2b — D-232 (D-213): filesystem-backed session-recording storage.
/// Streams both ways (never a whole-file <c>byte[]</c>): <see cref="SaveAsync"/>
/// copies the upload to disk, <see cref="OpenRead"/> hands back an async,
/// seekable <see cref="FileStream"/> the range-streaming endpoint reads from.
/// The stored file name is always <c>{sessionId:N}{ext}</c> — derived from our
/// own GUID, never from caller input, so there is no path-traversal surface.</summary>
public sealed class FilesystemSessionRecordingStorage(
    IOptions<SessionRecordingStorageOptions> options) : ISessionRecordingStorage
{
    private const int BufferSize = 81920;
    private readonly string _root = options.Value.RootPath;

    public async Task<string> SaveAsync(
        Guid sessionId, Stream content, string originalFileName,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var ext = Path.GetExtension(originalFileName);
        if (ext.Length is 0 or > 16) { ext = ".mp4"; }
        var fileName = $"{sessionId:N}{ext}";
        var fullPath = Path.Combine(_root, fileName);
        await using var dest = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
            BufferSize, FileOptions.Asynchronous);
        await content.CopyToAsync(dest, BufferSize, cancellationToken);
        return fileName;
    }

    public SessionRecordingStream? OpenRead(string storedFileName)
    {
        var fullPath = Path.Combine(_root, storedFileName);
        var info = new FileInfo(fullPath);
        if (!info.Exists) { return null; }
        var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous);
        return new SessionRecordingStream(stream, info.Length);
    }

    public Task DeleteAsync(
        string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, storedFileName);
        if (File.Exists(fullPath)) { File.Delete(fullPath); }
        return Task.CompletedTask;
    }
}
